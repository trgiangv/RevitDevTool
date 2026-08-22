namespace DevTools.Testing.Abstractions;

/// <summary>
/// Autodesk API paths written next to the test exe
/// (<c>$(TargetName).discovery-refs.txt</c>) from compile-only NuGet ReferencePath.
/// </summary>
public static class DiscoveryRefs
{
    public const string FileSuffix = ".discovery-refs.txt";

    public static string FilePathFor(string assemblyPath)
    {
        assemblyPath = Path.GetFullPath(assemblyPath);
        var directory = Path.GetDirectoryName(assemblyPath) ?? "";
        var name = Path.GetFileNameWithoutExtension(assemblyPath);
        return Path.Combine(directory, name + FileSuffix);
    }

    public static IReadOnlyDictionary<string, string> Read(string assemblyPath)
    {
        var file = FilePathFor(assemblyPath);
        if (!File.Exists(file))
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadAllLines(file))
        {
            var path = line.Trim();
            if (path.Length == 0 || !File.Exists(path) || IsTargetingPack(path))
                continue;

            map[Path.GetFileNameWithoutExtension(path)] = path;
        }

        return map;
    }

    /// <summary>
    /// Framework targeting packs are omitted here as well as in the MSBuild
    /// writer so an older package that still listed those paths stays safe.
    /// </summary>
    public static bool IsTargetingPack(string path) =>
        path.Contains(@"\Reference Assemblies\", StringComparison.OrdinalIgnoreCase)
        || path.Contains(@"\dotnet\packs\", StringComparison.OrdinalIgnoreCase);
}
