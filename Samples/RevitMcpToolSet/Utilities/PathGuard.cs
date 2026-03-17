using ModelContextProtocol;
namespace RevitMcpToolSet.Utilities;

internal static class PathGuard
{
    private static readonly HashSet<string> SystemDirectoriesToBlock = new(StringComparer.OrdinalIgnoreCase)
    {
        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
    };

    public static string SanitizeDirectoryPath(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            throw new McpException("Directory path cannot be null or empty.");

        directoryPath = directoryPath.Trim();
        if (directoryPath.Contains("..") || directoryPath.Contains("//") || directoryPath.Contains("\\\\"))
            throw new McpException($"Path traversal sequences not allowed in the path: '{directoryPath}'");

        try
        {
            var fullPath = Path.GetFullPath(directoryPath);
            if (fullPath.Contains(".."))
                throw new McpException($"Directory path resolved to invalid path: '{fullPath}'");
            if (!fullPath.EndsWith(Path.DirectorySeparatorChar.ToString()) && !fullPath.EndsWith(Path.AltDirectorySeparatorChar.ToString()) && File.Exists(fullPath))
                throw new McpException($"The path '{fullPath}' appears to be a file path. Please provide a directory path only.");
            if (SystemDirectoriesToBlock.Any(x => fullPath.StartsWith(x, StringComparison.OrdinalIgnoreCase)))
                throw new McpException($"The system directory path '{fullPath}' is not allowed for use.");
            return fullPath;
        }
        catch (McpException) { throw; }
        catch (ArgumentException) { throw new McpException($"Invalid directory path format: '{directoryPath}'."); }
        catch (NotSupportedException) { throw new McpException($"Directory path not supported: '{directoryPath}'."); }
        catch (PathTooLongException) { throw new McpException($"Directory path is too long: '{directoryPath}'."); }
    }

    public static string SanitizeFilePath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new McpException("File path cannot be null or empty.");

        filePath = filePath.Trim();
        if (filePath.Contains("..") || filePath.Contains("//") || filePath.Contains("\\\\"))
            throw new McpException($"Path traversal sequences not allowed in the path: '{filePath}'");

        try
        {
            var fullPath = Path.GetFullPath(filePath);
            if (fullPath.Contains(".."))
                throw new McpException($"File path resolved to invalid path: '{fullPath}'");
            if (SystemDirectoriesToBlock.Any(x => fullPath.StartsWith(x, StringComparison.OrdinalIgnoreCase)))
                throw new McpException($"The system directory path '{fullPath}' is not allowed for use.");
            return fullPath;
        }
        catch (McpException) { throw; }
        catch (ArgumentException) { throw new McpException($"Invalid file path format: '{filePath}'."); }
        catch (NotSupportedException) { throw new McpException($"File path not supported: '{filePath}'."); }
        catch (PathTooLongException) { throw new McpException($"File path is too long: '{filePath}'."); }
    }

    public static void CreateDirectory(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            throw new McpException("Directory path cannot be null or empty.");
        if (File.Exists(directoryPath))
            throw new McpException($"The path '{directoryPath}' appears to be a file path. Please provide a directory path only.");

        if (!Directory.Exists(directoryPath))
        {
            try
            {
                Directory.CreateDirectory(directoryPath);
                if (!Directory.Exists(directoryPath))
                    throw new McpException($"Directory creation appeared to succeed but directory '{directoryPath}' still does not exist.");
            }
            catch (UnauthorizedAccessException ex) { throw new McpException($"Access denied when creating directory '{directoryPath}': {ex.Message}"); }
            catch (DirectoryNotFoundException ex) { throw new McpException($"Parent directory not found for '{directoryPath}': {ex.Message}"); }
            catch (McpException) { throw; }
            catch (Exception ex) { throw new McpException($"Cannot create directory '{directoryPath}': {ex.Message}"); }
        }
    }

    public static string GenerateUniqueFilePath(string directoryPath, string baseName, string extension)
    {
        if (string.IsNullOrWhiteSpace(directoryPath)) throw new McpException("Directory path cannot be null or empty.");
        if (string.IsNullOrWhiteSpace(baseName)) throw new McpException("Base name cannot be null or empty.");
        if (string.IsNullOrWhiteSpace(extension)) throw new McpException("Extension cannot be null or empty.");

        var invalidChars = Path.GetInvalidFileNameChars().Concat([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]).ToArray();
        var sanitized = string.Join("_", baseName.Split(invalidChars));
        if (sanitized.Length > 200) sanitized = sanitized[..200];

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        extension = extension.TrimStart('.');
        var fileName = $"{sanitized}_{timestamp}.{extension}";
        var fullPath = Path.Combine(directoryPath, fileName);

        var counter = 1;
        while (File.Exists(fullPath))
        {
            fileName = $"{sanitized}_{timestamp}_{counter:D3}.{extension}";
            fullPath = Path.Combine(directoryPath, fileName);
            counter++;
            if (counter > 9999) throw new McpException($"Unable to generate unique file name after 9999 attempts for base name '{baseName}'");
        }
        return fullPath;
    }
}
