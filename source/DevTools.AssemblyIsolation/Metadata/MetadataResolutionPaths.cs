namespace DevTools.AssemblyIsolation.Metadata;

public static class MetadataResolutionPaths
{

    public static HashSet<string> Collect(string assemblyPath, params Type[] anchors)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddDllsFromDirectory(paths, Path.GetDirectoryName(assemblyPath));
        AddDllsFromDirectory(paths, Path.GetDirectoryName(typeof(object).Assembly.Location));
        foreach (var anchor in anchors)
            AddDllsFromDirectory(paths, Path.GetDirectoryName(anchor.Assembly.Location));

        foreach (var loaded in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (loaded.IsDynamic || string.IsNullOrEmpty(loaded.Location)) continue;
            paths.Add(loaded.Location);
        }

        return paths;
    }

    private static void AddDllsFromDirectory(HashSet<string> paths, string? directory)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return;
        try
        {
            foreach (var dll in Directory.GetFiles(directory, "*.dll"))
                paths.Add(dll);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
