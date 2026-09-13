using System.Reflection;
using DevTools.Testing.Host.NUnit.Loading;

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
        NUnitGenerationPolicy.FrameworkAssemblyFileName);

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

    public static string PrivateMicrosoftExtensionsStubPath { get; } = Path.Combine(
        NUnitGenerationTestEnvironment.RepositoryRoot,
        "tests",
        "DevTools.NUnit.Host.Tests",
        "Loading",
        "Stubs",
        "PrivateMicrosoftExtensions",
        "bin",
        "Debug",
        "net10.0",
        "Microsoft.Extensions.Logging.Abstractions.dll");

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
            "DevTools.nunit.conflict-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(isolatedCopyDirectory);
        var isolatedCopyPath = Path.Combine(isolatedCopyDirectory, NUnitGenerationPolicy.FrameworkAssemblyFileName);
        File.Copy(ConflictingNUnitStubPath, isolatedCopyPath, overwrite: true);

        var loaded = Assembly.Load(File.ReadAllBytes(isolatedCopyPath));
        Assert.AreEqual(new Version(3, 14, 0, 0), loaded.GetName().Version);

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
            "DevTools.nunit.private-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(isolatedCopyDirectory);
        var isolatedCopyPath = Path.Combine(isolatedCopyDirectory, "GenerationPrivateDependency.dll");
        File.Copy(GenerationPrivateDependencyStubPath, isolatedCopyPath, overwrite: true);

        var loaded = Assembly.Load(File.ReadAllBytes(isolatedCopyPath));
        Assert.AreEqual("GenerationPrivateDependency", loaded.GetName().Name, StringComparer.OrdinalIgnoreCase);
        Assert.AreEqual(new Version(1, 0, 0, 0), loaded.GetName().Version);

        return loaded;
    }
}
