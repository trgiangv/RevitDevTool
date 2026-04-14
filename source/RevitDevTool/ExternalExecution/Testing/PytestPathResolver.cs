using System.IO;

namespace RevitDevTool.ExternalExecution.Testing;

internal static class PytestPathResolver
{
    public static string ResolveWorkspaceRoot(string workspaceRoot, string testRoot)
    {
        if (!string.IsNullOrWhiteSpace(workspaceRoot))
            return Path.GetFullPath(workspaceRoot);

        if (Directory.Exists(testRoot))
            return Path.GetFullPath(testRoot);

        var directory = Path.GetDirectoryName(testRoot);
        return Path.GetFullPath(string.IsNullOrWhiteSpace(directory) ? testRoot : directory);
    }

    public static string ResolvePath(string path, string workspaceRoot)
    {
        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(workspaceRoot, path));
    }
}
