using DevTools.AssemblyIsolation;
using DevTools.Testing.Abstractions.Runtime;

namespace DevTools.Testing.Host.Loading;

public static class TestingGenerationFiles
{
    public static bool IsManagedIdentity(string path) =>
        IsManagedAssembly(path) && !IsSatelliteResourceAssembly(path);

    public static bool TryGetManagedAssemblyIdentity(string path, out string? simpleName)
    {
        simpleName = null;
        var extension = Path.GetExtension(path);
        if (!extension.Equals(".dll", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!ManagedAssembly.TryGetName(path, out var identity))
            return false;

        simpleName = identity.Name;
        return !string.IsNullOrWhiteSpace(simpleName);
    }

    public static bool IsManagedAssembly(string path) =>
        TryGetManagedAssemblyIdentity(path, out _);

    public static string RequireManagedAssembly(string? assemblyPath)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath))
            throw new TestingGenerationBuildException("Assembly path is required.");

        var fullPath = Path.GetFullPath(assemblyPath);
        if (!File.Exists(fullPath))
            throw new TestingGenerationBuildException($"Assembly not found: {fullPath}");
        if (!IsManagedAssembly(fullPath))
            throw new TestingGenerationBuildException($"Failed to read assembly metadata: {fullPath}");

        return fullPath;
    }

    public static string GetRelativePath(string relativeTo, string path)
    {
        var relativeToUri = new Uri(AppendDirectorySeparator(relativeTo));
        var pathUri = new Uri(path);
        var relativeUri = relativeToUri.MakeRelativeUri(pathUri);
        return Uri.UnescapeDataString(
            relativeUri.ToString().Replace('/', Path.DirectorySeparatorChar));
    }

    public static bool IsVolatileGenerationOutput(string relativePath)
    {
        var root = relativePath.Split('\\')[0];
        if (root.Equals("Log", StringComparison.OrdinalIgnoreCase)
            || root.Equals("TestResults", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var extension = Path.GetExtension(relativePath);
        return extension.Equals(".diag", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".log", StringComparison.OrdinalIgnoreCase);
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

    public static bool IsSharedTestingContract(string path)
    {
        if (!TryGetManagedAssemblyIdentity(path, out var simpleName))
            return false;

        return string.Equals(
            simpleName,
            typeof(ITestingRuntimeSession).Assembly.GetName().Name,
            StringComparison.OrdinalIgnoreCase);
    }

    public static Dictionary<string, (string SourcePath, string RelativePath)> ScanOutputDirectory(string outputDirectory)
    {
        outputDirectory = Path.GetFullPath(outputDirectory);
        var files = new Dictionary<string, (string SourcePath, string RelativePath)>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.EnumerateFiles(outputDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = GetRelativePath(outputDirectory, path);
            if (IsVolatileGenerationOutput(relativePath)
                || IsSharedTestingContract(path))
            {
                continue;
            }

            files[relativePath] = (path, relativePath);
        }

        return files;
    }

    private static bool IsSatelliteResourceAssembly(string path)
    {
        if (!ManagedAssembly.TryGetName(path, out var identity))
            return false;

        return identity.Name?.EndsWith(".resources", StringComparison.OrdinalIgnoreCase) == true
               && !string.IsNullOrWhiteSpace(identity.CultureName);
    }
}
