using System.Reflection;
using DevTools.NUnit.Host.Loading;

namespace DevTools.NUnit.Host.Tests.Loading;

internal static class NUnitRuntimeUnloadTestHelper
{
    public static string ConflictingNUnitStubPath { get; } = Path.Combine(
        NUnitGenerationTestEnvironment.RepositoryRoot,
        "tests",
        "DevTools.NUnit.Host.Tests",
        "Loading",
        "Stubs",
        "ConflictingNUnitFramework",
        "bin",
        "Debug",
        "net10.0",
        NUnitGenerationBuilder.FrameworkAssemblyFileName);

    public static string GenerationPrivateDependencyStubPath { get; } = Path.Combine(
        NUnitGenerationTestEnvironment.RepositoryRoot,
        "tests",
        "DevTools.NUnit.Host.Tests",
        "Loading",
        "Stubs",
        "GenerationPrivateDependency",
        "bin",
        "Debug",
        "net10.0",
        "GenerationPrivateDependency.dll");

    internal static Assembly LoadConflictingNUnitIntoDefaultContext()
    {
        if (!File.Exists(ConflictingNUnitStubPath))
        {
            throw new FileNotFoundException(
                $"Conflicting NUnit stub was not built: {ConflictingNUnitStubPath}",
                ConflictingNUnitStubPath);
        }

        var isolatedCopyDirectory = Path.Combine(
            Path.GetTempPath(),
            "DevTools",
            "NUnit",
            "ConflictingDefault",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(isolatedCopyDirectory);
        var isolatedCopyPath = Path.Combine(isolatedCopyDirectory, NUnitGenerationBuilder.FrameworkAssemblyFileName);
        File.Copy(ConflictingNUnitStubPath, isolatedCopyPath, overwrite: true);

        var loaded = Assembly.Load(File.ReadAllBytes(isolatedCopyPath));
        Assert.Equal(new Version(3, 14, 0, 0), loaded.GetName().Version);

        return loaded;
    }

    internal static Assembly LoadGenerationPrivateDependencyIntoDefaultContext()
    {
        if (!File.Exists(GenerationPrivateDependencyStubPath))
        {
            throw new FileNotFoundException(
                $"Generation private dependency stub was not built: {GenerationPrivateDependencyStubPath}",
                GenerationPrivateDependencyStubPath);
        }

        var isolatedCopyDirectory = Path.Combine(
            Path.GetTempPath(),
            "DevTools",
            "NUnit",
            "PrivateDefault",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(isolatedCopyDirectory);
        var isolatedCopyPath = Path.Combine(isolatedCopyDirectory, "GenerationPrivateDependency.dll");
        File.Copy(GenerationPrivateDependencyStubPath, isolatedCopyPath, overwrite: true);

        var loaded = Assembly.Load(File.ReadAllBytes(isolatedCopyPath));
        Assert.Equal("GenerationPrivateDependency", loaded.GetName().Name, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(new Version(1, 0, 0, 0), loaded.GetName().Version);

        return loaded;
    }
}
