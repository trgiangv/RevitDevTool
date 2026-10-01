using System.Reflection;

namespace DevTools.Testing.Host.Loading;

/// <summary>
/// Shared file-name and version checks for generation policies. Each policy
/// still owns its file names and expected versions.
/// </summary>
internal static class TestingGenerationPins
{
    public static bool IsNamed(string path, string fileName) =>
        string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase);

    public static string GetNamedAssembly(TestingGenerationManifest manifest, string fileName) =>
        manifest.ManagedAssemblies.Single(path => IsNamed(path, fileName));

    public static void ValidateAssemblyVersion(
        string path,
        string fileName,
        Version expected,
        string? sourceOutputDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Version? version;
        try
        {
            version = AssemblyName.GetAssemblyName(path).Version;
        }
        catch (Exception ex) when (ex is BadImageFormatException or FileLoadException)
        {
            throw new TestingGenerationBuildException(
                $"{fileName} is not a valid managed assembly: {path}");
        }

        if (version != expected)
        {
            throw new TestingGenerationBuildException(
                $"Expected {fileName} {expected}; found {version?.ToString() ?? "<missing>"} at {Describe(path, sourceOutputDirectory)}.");
        }
    }

    public static void ValidateFileVersion(
        string path,
        string fileName,
        string expectedFileVersion,
        string expectedText,
        string? sourceOutputDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        TestingGenerationFiles.TryGetFileVersion(path, out var fileVersion);
        if (!string.Equals(fileVersion, expectedFileVersion, StringComparison.Ordinal))
        {
            throw new TestingGenerationBuildException(
                $"Expected {fileName} {expectedText}; found {fileVersion ?? "<missing>"} at {Describe(path, sourceOutputDirectory)}.");
        }

        if (!TestingGenerationFiles.IsManagedAssembly(path))
        {
            throw new TestingGenerationBuildException(
                $"{fileName} is not a valid managed assembly: {path}");
        }
    }

    public static void ValidateConsumerPins(
        IEnumerable<string> paths,
        string outputDirectory,
        IReadOnlyList<GenerationPin> pins)
    {
        foreach (var path in paths)
        {
            foreach (var pin in pins)
            {
                if (!IsNamed(path, pin.FileName))
                    continue;

                pin.Validate(path, outputDirectory);
                break;
            }
        }
    }

    public static string Describe(string path, string? sourceOutputDirectory) =>
        sourceOutputDirectory is null
            ? path
            : TestingGenerationFiles.GetRelativePath(sourceOutputDirectory, path);
}

internal readonly record struct GenerationPin(
    string FileName,
    Version? AssemblyVersion = null,
    string? FileVersion = null,
    string? PackageVersion = null,
    bool AllowMany = false)
{
    public string CountLabel =>
        PackageVersion ?? AssemblyVersion?.ToString() ?? FileVersion
        ?? throw new InvalidOperationException($"Pin {FileName} has no expected version.");

    public void Validate(string path, string? sourceOutputDirectory)
    {
        if (FileVersion is not null)
        {
            var expectedText = PackageVersion is null
                ? $"file version {FileVersion}"
                : $"file version {FileVersion} (package {PackageVersion})";
            TestingGenerationPins.ValidateFileVersion(
                path,
                FileName,
                FileVersion,
                expectedText,
                sourceOutputDirectory);
            return;
        }

        if (AssemblyVersion is null)
            throw new InvalidOperationException($"Pin {FileName} has no expected version.");

        TestingGenerationPins.ValidateAssemblyVersion(path, FileName, AssemblyVersion, sourceOutputDirectory);
    }
}
