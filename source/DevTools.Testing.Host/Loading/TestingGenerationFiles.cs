using System.Reflection;
using DevTools.Testing.Abstractions.Runtime;

namespace DevTools.Testing.Host.Loading;

public static class TestingGenerationFiles
{
    public static TestingGenerationFileKind Classify(string path)
    {
        if (string.Equals(Path.GetExtension(path), ".pdb", StringComparison.OrdinalIgnoreCase))
            return TestingGenerationFileKind.Symbols;
        if (IsSatelliteResourceAssembly(path))
            return TestingGenerationFileKind.Other;
        if (IsManagedAssembly(path))
            return TestingGenerationFileKind.Managed;
        return string.Equals(Path.GetExtension(path), ".dll", StringComparison.OrdinalIgnoreCase)
            ? TestingGenerationFileKind.Native
            : TestingGenerationFileKind.Other;
    }

    public static bool TryGetManagedAssemblyIdentity(string path, out string? simpleName)
    {
        simpleName = null;
        var extension = Path.GetExtension(path);
        if (!extension.Equals(".dll", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            simpleName = AssemblyName.GetAssemblyName(path).Name;
            return !string.IsNullOrWhiteSpace(simpleName);
        }
        catch (BadImageFormatException)
        {
            return false;
        }
        catch (FileLoadException)
        {
            return false;
        }
    }

    public static bool IsManagedAssembly(string path) =>
        TryGetManagedAssemblyIdentity(path, out _);

    public static string NormalizeRelativePath(string relativePath) =>
        TestingGenerationPaths.NormalizeRelativePath(relativePath);

    public static string GetRelativePath(string relativeTo, string path) =>
        TestingGenerationPaths.GetRelativePath(relativeTo, path);

    public static bool IsVolatileGenerationOutput(string relativePath) =>
        TestingGenerationPaths.IsVolatileGenerationOutput(relativePath);

    public static bool IsSharedTestingContract(string path)
    {
        if (!TryGetManagedAssemblyIdentity(path, out var simpleName))
            return false;

        return string.Equals(
            simpleName,
            typeof(ITestingRuntimeSession).Assembly.GetName().Name,
            StringComparison.OrdinalIgnoreCase);
    }

    public static Dictionary<string, TestingGenerationFile> ScanOutputDirectory(string outputDirectory)
    {
        outputDirectory = Path.GetFullPath(outputDirectory);
        var files = new Dictionary<string, TestingGenerationFile>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.EnumerateFiles(outputDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = NormalizeRelativePath(GetRelativePath(outputDirectory, path));
            if (IsVolatileGenerationOutput(relativePath)
                || IsSharedTestingContract(path))
            {
                continue;
            }

            files[relativePath] = new TestingGenerationFile(path, relativePath, Classify(path));
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
        if (!firstInfo.Exists || !secondInfo.Exists)
            return false;
        if (firstInfo.Length != secondInfo.Length)
            return false;

        using var first = new FileStream(firstPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var second = new FileStream(secondPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
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
            for (var i = 0; i < firstRead; i++)
            {
                if (buffer[i] != other[i])
                    return false;
            }
        }
    }

    public static void MergeFile(
        IDictionary<string, TestingGenerationFile> files,
        string sourcePath,
        string relativePath)
    {
        relativePath = NormalizeRelativePath(relativePath);
        if (files.TryGetValue(relativePath, out var existing))
        {
            if (ContentEquals(existing.SourcePath, sourcePath))
                return;

            files[relativePath] = new TestingGenerationFile(sourcePath, relativePath, Classify(sourcePath));
            return;
        }

        files[relativePath] = new TestingGenerationFile(sourcePath, relativePath, Classify(sourcePath));
    }

    private static bool IsSatelliteResourceAssembly(string path)
    {
        if (!IsManagedAssembly(path))
            return false;

        var identity = AssemblyName.GetAssemblyName(path);
        return identity.Name?.EndsWith(".resources", StringComparison.OrdinalIgnoreCase) == true
               && !string.IsNullOrWhiteSpace(identity.CultureName);
    }
}
