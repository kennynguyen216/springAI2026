/// <summary>
/// Resolves loose filenames to the user's common document folders.
/// </summary>
public static class FileLocator
{
    /// <summary>
    /// Resolves a file path by checking the provided path first and then searching common folders.
    /// </summary>
    /// <param name="filePath">The original file path or file name.</param>
    /// <param name="extension">The expected file extension.</param>
    /// <returns>The resolved file path when found, or the original input when not found.</returns>
    public static string ResolveFilePath(string filePath, string extension)
    {
        if (File.Exists(filePath))
        {
            return filePath;
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var searchFolders = new[]
        {
            Path.Combine(userProfile, "Downloads"),
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
        };

        var fileName = Path.GetFileName(filePath);
        if (!fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
        {
            fileName += extension;
        }

        foreach (var folder in searchFolders)
        {
            var candidate = Path.Combine(folder, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            var found = Directory
                .GetFiles(folder, $"*{Path.GetFileNameWithoutExtension(fileName)}*{extension}", SearchOption.TopDirectoryOnly)
                .FirstOrDefault();

            if (found is not null)
            {
                return found;
            }
        }

        return filePath;
    }
}
