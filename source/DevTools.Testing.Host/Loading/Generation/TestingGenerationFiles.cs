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

    public static bool TryGetFileVersion(string path, out string? fileVersion)
    {
        fileVersion = System.Diagnostics.FileVersionInfo.GetVersionInfo(path).FileVersion;
        return fileVersion is not null;
    }

    public static bool ContentEquals(string firstPath, string secondPath)
    {
        var firstInfo = new FileInfo(firstPath);
        var secondInfo = new FileInfo(secondPath);
        if (!HasSameFileShape(firstInfo, secondInfo))
            return false;

        using var first = new FileStream(firstPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var second = new FileStream(secondPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return StreamsEqual(first, second);
    }

    private static bool HasSameFileShape(FileInfo first, FileInfo second) =>
        first.Exists && second.Exists && first.Length == second.Length;

    private static bool StreamsEqual(Stream first, Stream second)
    {
        var buffer = new byte[81920];
        var other = new byte[81920];
        while (true)
        {
            var firstRead = first.Read(buffer, 0, buffer.Length);
            var secondRead = second.Read(other, 0, other.Length);
            if (firstRead != secondRead)
                return false;
            if (firstRead == 0)
                return true;

            if (!BuffersEqual(buffer, other, firstRead))
                return false;
        }
    }

    private static bool BuffersEqual(byte[] first, byte[] second, int count)
    {
        for (var i = 0; i < count; i++)
        {
            if (first[i] != second[i])
                return false;
        }

        return true;
    }

    public static void MergeFile(
        IDictionary<string, (string SourcePath, string RelativePath)> files,
        string sourcePath,
        string relativePath)
    {
        if (files.TryGetValue(relativePath, out var existing)
            && ContentEquals(existing.SourcePath, sourcePath))
            return;

        files[relativePath] = (sourcePath, relativePath);
    }

    /// <summary>
    /// Adds a runtime-closure file to the generation. Same as <see cref="MergeFile"/>
    /// except it never replaces a consumer assembly with an older version of the same
    /// identity. The consumer test assembly was compiled against its own closure (for
    /// example System.Threading.Tasks.Extensions 4.2.4.0 for <c>ValueTask</c>); an older
    /// runtime copy cannot satisfy those references and splits type identity on net48.
    /// </summary>
    public static void MergeRuntimeDependency(
        IDictionary<string, (string SourcePath, string RelativePath)> files,
        string sourcePath,
        string relativePath)
    {
        if (files.TryGetValue(relativePath, out var existing)
            && IsNewerSameIdentity(existing.SourcePath, sourcePath))
        {
            return;
        }

        MergeFile(files, sourcePath, relativePath);
    }

    private static bool IsNewerSameIdentity(string existingPath, string incomingPath)
    {
        if (!ManagedAssembly.TryGetName(existingPath, out var existing)
            || !ManagedAssembly.TryGetName(incomingPath, out var incoming))
        {
            return false;
        }

        return string.Equals(existing.Name, incoming.Name, StringComparison.OrdinalIgnoreCase)
               && (existing.GetPublicKeyToken() ?? []).AsSpan().SequenceEqual(incoming.GetPublicKeyToken() ?? [])
               && existing.Version is not null
               && incoming.Version is not null
               && existing.Version > incoming.Version;
    }

    private static bool IsSatelliteResourceAssembly(string path)
    {
        if (!ManagedAssembly.TryGetName(path, out var identity))
            return false;

        return identity.Name?.EndsWith(".resources", StringComparison.OrdinalIgnoreCase) == true
               && !string.IsNullOrWhiteSpace(identity.CultureName);
    }
}
