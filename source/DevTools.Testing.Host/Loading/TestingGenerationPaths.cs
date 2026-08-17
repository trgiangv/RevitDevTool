namespace DevTools.Testing.Host.Loading;

internal static class TestingGenerationPaths
{
    internal const string GenerationCompleteMarkerFileName = ".generation-complete";

    internal static bool IsVolatileGenerationOutput(string relativePath)
    {
        var normalized = NormalizeRelativePath(relativePath);
        var root = normalized.Split('\\')[0];
        if (root.Equals("Log", StringComparison.OrdinalIgnoreCase)
            || root.Equals("TestResults", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var extension = Path.GetExtension(normalized);
        return extension.Equals(".diag", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".log", StringComparison.OrdinalIgnoreCase);
    }

    internal static string NormalizeRelativePath(string relativePath) =>
        relativePath.Replace('/', '\\');

    internal static string GetRelativePath(string relativeTo, string path)
    {
        var relativeToUri = new Uri(AppendDirectorySeparator(relativeTo));
        var pathUri = new Uri(path);
        var relativeUri = relativeToUri.MakeRelativeUri(pathUri);
        return Uri.UnescapeDataString(
            relativeUri.ToString().Replace('/', Path.DirectorySeparatorChar));
    }

    private static string AppendDirectorySeparator(string path)
    {
        if (!path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            && !path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
        {
            return path + Path.DirectorySeparatorChar;
        }

        return path;
    }
}
