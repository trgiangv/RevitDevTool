using System.IO;

namespace DevTools.Execution.External.Testing;

/// <summary>
/// Nodeid path helpers. Pytest filename conventions live in the client plugin, not here.
/// </summary>
public static class IpyTestPath
{
    public static string FileFromNodeid(string nodeid)
    {
        var index = nodeid.IndexOf("::", StringComparison.Ordinal);
        return index < 0 ? nodeid : nodeid[..index];
    }

    public static string ToNodeidPrefix(string fullPath, string workspaceRoot)
    {
        var relative = Path.GetRelativePath(workspaceRoot, fullPath);
        return relative.Replace('\\', '/');
    }
}
