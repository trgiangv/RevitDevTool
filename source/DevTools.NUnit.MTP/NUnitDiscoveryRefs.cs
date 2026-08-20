namespace DevTools.NUnit.MTP;

/// <summary>
/// NuGet compile-only paths written next to the test exe
/// (<c>$(TargetName).discovery-refs.txt</c>). MSBuild already resolved
/// these from packages with Copy Local false; testhost has no
/// <c>ReferencePath</c>.
/// </summary>
internal static class NUnitDiscoveryRefs
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

    internal static bool IsTargetingPack(string path) =>
        path.Contains(@"\Reference Assemblies\", StringComparison.OrdinalIgnoreCase)
        || path.Contains(@"\dotnet\packs\", StringComparison.OrdinalIgnoreCase);
}
