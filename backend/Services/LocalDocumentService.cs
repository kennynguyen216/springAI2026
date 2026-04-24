using System.Text;
using Microsoft.AspNetCore.Hosting;
using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;

public sealed class LocalDocumentService
{
    private static readonly string[] DefaultExtensions = [".pdf", ".docx", ".txt", ".md", ".rtf"];
    private readonly string[] _roots;

    public LocalDocumentService(IWebHostEnvironment environment, IEnumerable<string>? searchRoots = null)
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var roots = searchRoots ?? new[]
        {
            environment.ContentRootPath,
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Path.Combine(userProfile, "Downloads")
        };

        _roots = roots
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<string> SearchRoots => _roots;

    public IReadOnlyList<LocalDocumentMatch> FindRecentDocuments(string keyword, int maxResults, IEnumerable<string>? extensions = null)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return [];
        }

        var normalizedExtensions = (extensions ?? DefaultExtensions)
            .Where(extension => !string.IsNullOrWhiteSpace(extension))
            .Select(extension => extension.StartsWith('.') ? extension : $".{extension}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var keywords = keyword
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.ToLowerInvariant())
            .ToArray();

        var results = new List<LocalDocumentMatch>();
        foreach (var root in _roots.Where(Directory.Exists))
        {
            foreach (var file in EnumerateFilesSafe(root))
            {
                var extension = Path.GetExtension(file);
                if (!normalizedExtensions.Contains(extension))
                {
                    continue;
                }

                var fileName = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                if (!keywords.All(fileName.Contains))
                {
                    continue;
                }

                var info = new FileInfo(file);
                results.Add(new LocalDocumentMatch(
                    info.FullName,
                    info.Name,
                    info.Extension,
                    info.LastWriteTimeUtc,
                    info.Length));
            }
        }

        return results
            .GroupBy(result => result.FullPath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(result => result.LastWriteTimeUtc)
            .ThenBy(result => result.Name, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(maxResults, 1, 20))
            .ToArray();
    }

    public string ReadDocumentText(string filePath)
    {
        try
        {
            var resolvedPath = ResolveDocumentPath(filePath);
            if (resolvedPath is null)
            {
                return $"I couldn't find a local document matching '{filePath}'.";
            }

            var extension = Path.GetExtension(resolvedPath).ToLowerInvariant();
            return extension switch
            {
                ".pdf" => ReadPdf(resolvedPath),
                ".docx" => ReadWord(resolvedPath),
                ".txt" or ".md" or ".rtf" => File.ReadAllText(resolvedPath),
                _ => $"Unsupported document type: {extension}"
            };
        }
        catch (Exception ex)
        {
            return $"Error reading document: {ex.Message}";
        }
    }

    public string? ResolveDocumentPath(string filePathOrName)
    {
        if (string.IsNullOrWhiteSpace(filePathOrName))
        {
            return null;
        }

        if (Path.IsPathRooted(filePathOrName) && File.Exists(filePathOrName))
        {
            return Path.GetFullPath(filePathOrName);
        }

        var normalizedInput = filePathOrName.Trim();
        var fileName = Path.GetFileName(normalizedInput);
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);

        if (!string.IsNullOrWhiteSpace(baseName))
        {
            var exactNameMatches = FindRecentDocuments(fileName, 10, string.IsNullOrWhiteSpace(extension) ? null : [extension])
                .Where(match => string.Equals(match.Name, fileName, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (exactNameMatches.Length > 0)
            {
                return exactNameMatches[0].FullPath;
            }

            var baseNameMatches = FindRecentDocuments(baseName, 10, string.IsNullOrWhiteSpace(extension) ? null : [extension]);
            if (baseNameMatches.Count > 0)
            {
                return baseNameMatches[0].FullPath;
            }
        }

        return null;
    }

    public string DescribeMostRecentDocument(string keyword)
    {
        var match = FindRecentDocuments(keyword, 1).FirstOrDefault();
        if (match is null)
        {
            return $"I couldn't find any recent documents matching '{keyword}'.";
        }

        return $"Most recent match for '{keyword}': {match.Name}\nPath: {match.FullPath}\nLast modified (UTC): {match.LastWriteTimeUtc:yyyy-MM-dd HH:mm:ss}";
    }

    public string DescribeDocumentByName(string filePathOrName)
    {
        var resolvedPath = ResolveDocumentPath(filePathOrName);
        if (resolvedPath is null)
        {
            return $"I couldn't find a local document matching '{filePathOrName}'.";
        }

        var info = new FileInfo(resolvedPath);
        return $"Found local document: {info.Name}\nPath: {info.FullName}\nLast modified (UTC): {info.LastWriteTimeUtc:yyyy-MM-dd HH:mm:ss}\nSize: {info.Length} bytes";
    }

    private static IEnumerable<string> EnumerateFilesSafe(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            string[] files;

            try
            {
                files = Directory.GetFiles(current);
            }
            catch
            {
                continue;
            }

            foreach (var file in files)
            {
                yield return file;
            }

            string[] directories;
            try
            {
                directories = Directory.GetDirectories(current);
            }
            catch
            {
                continue;
            }

            foreach (var directory in directories)
            {
                pending.Push(directory);
            }
        }
    }

    private static string ReadPdf(string filePath)
    {
        using var pdf = PdfDocument.Open(filePath);
        var text = string.Join("\n", pdf.GetPages().Select(page => page.Text));
        return string.IsNullOrWhiteSpace(text) ? "The PDF is empty." : text;
    }

    private static string ReadWord(string filePath)
    {
        using var doc = WordprocessingDocument.Open(filePath, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        return body?.InnerText ?? "The Word document is empty or unreadable.";
    }
}

public sealed record LocalDocumentMatch(
    string FullPath,
    string Name,
    string Extension,
    DateTime LastWriteTimeUtc,
    long SizeBytes);

public static class DocumentQueryRouter
{
    private static readonly string[] SupportedExtensions = [".pdf", ".docx", ".txt", ".md", ".rtf"];
    private static readonly string[] DocumentActionWords = ["find", "open", "read", "retrieve", "get", "show", "locate", "lookup", "look"];

    public static bool TryGetMostRecentKeyword(string input, out string keyword)
    {
        keyword = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var normalized = input.Trim();
        var markers = new[]
        {
            "most recent ",
            "latest ",
            "newest "
        };

        foreach (var marker in markers)
        {
            var index = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                continue;
            }

            var candidate = normalized[(index + marker.Length)..]
                .Trim()
                .TrimEnd('?', '.', '!');

            if (candidate.StartsWith("the ", StringComparison.OrdinalIgnoreCase))
            {
                candidate = candidate[4..];
            }

            if (candidate.EndsWith(" file", StringComparison.OrdinalIgnoreCase))
            {
                candidate = candidate[..^5];
            }

            keyword = candidate.Trim();
            return !string.IsNullOrWhiteSpace(keyword);
        }

        return false;
    }

    public static bool TryGetReferencedDocumentName(string input, out string fileName)
    {
        fileName = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        foreach (var token in input.Split([' ', '\n', '\r', '\t', '"', '\'', '(', ')', '[', ']', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var trimmed = token.Trim().TrimEnd('?', '.', '!', ';', ':');
            var extension = Path.GetExtension(trimmed);
            if (!string.IsNullOrWhiteSpace(extension) &&
                SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                fileName = Path.GetFileName(trimmed);
                return !string.IsNullOrWhiteSpace(fileName);
            }
        }

        return false;
    }

    public static bool TryGetBareDocumentReference(string input, out string documentName)
    {
        documentName = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var normalized = input.Trim();
        if (normalized.Contains('.') && TryGetReferencedDocumentName(normalized, out var explicitFileName))
        {
            documentName = explicitFileName;
            return true;
        }

        var tokens = normalized.Split([' ', '\n', '\r', '\t', '"', '\'', '(', ')', '[', ']', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
        {
            return false;
        }

        var hasDocumentVerb = tokens.Any(token => DocumentActionWords.Contains(token.Trim().ToLowerInvariant()));
        if (!hasDocumentVerb)
        {
            return false;
        }

        for (var index = 0; index < tokens.Length; index++)
        {
            var candidate = tokens[index].Trim().TrimEnd('?', '.', '!', ';', ':');
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var lower = candidate.ToLowerInvariant();
            if (DocumentActionWords.Contains(lower) ||
                lower is "document" or "file" or "named" or "called" or "me" or "for" or "the" or "a" or "an")
            {
                continue;
            }

            if (candidate.Contains('/') || candidate.Contains('\\'))
            {
                documentName = Path.GetFileNameWithoutExtension(candidate);
                return !string.IsNullOrWhiteSpace(documentName);
            }

            if (candidate.Contains('_') || candidate.Contains('-'))
            {
                documentName = Path.GetFileNameWithoutExtension(candidate);
                return !string.IsNullOrWhiteSpace(documentName);
            }
        }

        return false;
    }
}
