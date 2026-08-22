using System.Reflection;
using DevTools.Testing.Abstractions.Runtime;

namespace DevTools.Testing.Host.Loading;

internal static class TestingGenerationFiles
{
    internal static TestingGenerationFileKind Classify(string path)
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

    internal static bool TryGetManagedAssemblyIdentity(string path, out string? simpleName)
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

    internal static bool IsManagedAssembly(string path) =>
        TryGetManagedAssemblyIdentity(path, out _);

    internal static bool IsSharedTestingContract(string path)
    {
        if (!TryGetManagedAssemblyIdentity(path, out var simpleName))
            return false;

        return string.Equals(
            simpleName,
            typeof(ITestingRuntimeSession).Assembly.GetName().Name,
            StringComparison.OrdinalIgnoreCase);
    }

    internal static Dictionary<string, TestingGenerationFile> ScanOutputDirectory(string outputDirectory)
    {
        outputDirectory = Path.GetFullPath(outputDirectory);
        var files = new Dictionary<string, TestingGenerationFile>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.EnumerateFiles(outputDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = TestingGenerationPaths.NormalizeRelativePath(
                Path.GetRelativePath(outputDirectory, path));
            if (TestingGenerationPaths.IsVolatileGenerationOutput(relativePath)
                || IsSharedTestingContract(path))
            {
                continue;
            }

            files[relativePath] = new TestingGenerationFile(path, relativePath, Classify(path));
        }

        return files;
    }

    internal static void ValidateManagedFrameworkVersion(
        string frameworkPath,
        string frameworkAssemblyFileName,
        string expectedFileVersion,
        string expectedPackageVersion,
        string? sourceOutputDirectory,
        Func<string, Exception> throwInvalid)
    {
        var fileVersion = System.Diagnostics.FileVersionInfo.GetVersionInfo(frameworkPath).FileVersion;
        if (!string.Equals(fileVersion, expectedFileVersion, StringComparison.Ordinal))
        {
            var location = sourceOutputDirectory is null
                ? frameworkPath
                : TestingGenerationPaths.NormalizeRelativePath(
                    Path.GetRelativePath(sourceOutputDirectory, frameworkPath));
            throw throwInvalid(
                $"Expected {frameworkAssemblyFileName} file version {expectedFileVersion} (package {expectedPackageVersion}); found {fileVersion ?? "<missing>"} at {location}.");
        }

        if (!IsManagedAssembly(frameworkPath))
        {
            throw throwInvalid(
                $"{frameworkAssemblyFileName} is not a valid managed assembly: {frameworkPath}");
        }
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
