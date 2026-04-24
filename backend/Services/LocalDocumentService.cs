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
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".pdf" => ReadPdf(filePath),
                ".docx" => ReadWord(filePath),
                ".txt" or ".md" or ".rtf" => File.ReadAllText(filePath),
                _ => $"Unsupported document type: {extension}"
            };
        }
        catch (Exception ex)
        {
            return $"Error reading document: {ex.Message}";
        }
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
}
