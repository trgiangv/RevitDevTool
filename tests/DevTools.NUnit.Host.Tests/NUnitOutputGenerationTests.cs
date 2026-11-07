using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.NUnit.Host.Tests;

[TestClass]
public sealed class NUnitOutputGenerationTests
{
    [TestMethod]
    public void Platform_dependencies_stay_private()
    {
        using var workspace = new TempWorkspace();
        var testAssembly = NUnitGenerationTestEnvironment.CreateFixtureWorkspace(workspace.Root, "private", output =>
        {
            File.Copy(typeof(Microsoft.Extensions.Logging.Abstractions.NullLogger).Assembly.Location,
                Path.Combine(output, "Microsoft.Extensions.Logging.Abstractions.dll"), true);
            File.Copy(typeof(NUnitOutputGenerationTests).Assembly.Location,
                Path.Combine(output, "System.Custom.dll"), true);
        });

        var manifest = NUnitGenerationTestEnvironment.CreateBuilder(
            NUnitGenerationTestEnvironment.CreateIsolatedGenerationsRoot()).Build(testAssembly);

        Assert.IsTrue(manifest.ManagedAssemblies.Any(path => path.EndsWith("Microsoft.Extensions.Logging.Abstractions.dll", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(manifest.ManagedAssemblies.Any(path => path.EndsWith("System.Custom.dll", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void Renamed_contract_assembly_is_excluded()
    {
        using var workspace = new TempWorkspace();
        var testAssembly = NUnitGenerationTestEnvironment.CreateFixtureWorkspace(workspace.Root, "contract", output =>
            File.Copy(typeof(TestRunRequest).Assembly.Location,
                Path.Combine(output, "PrivateTestingContract.dll"), true));

        var manifest = NUnitGenerationTestEnvironment.CreateBuilder(
            NUnitGenerationTestEnvironment.CreateIsolatedGenerationsRoot()).Build(testAssembly);

        Assert.IsFalse(manifest.ManagedAssemblies.Any(path => path.EndsWith("PrivateTestingContract.dll", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void Satellites_stay_out_of_managed_list()
    {
        using var workspace = new TempWorkspace();
        const string culture = "fr";
        const string resourceFile = "Microsoft.Testing.Extensions.MSBuild.resources.dll";
        var source = Path.Combine(AppContext.BaseDirectory, culture, resourceFile);
        Assert.IsTrue(File.Exists(source), $"Satellite fixture was not found: {source}");
        var testAssembly = NUnitGenerationTestEnvironment.CreateFixtureWorkspace(workspace.Root, "satellite", output =>
        {
            var cultureDirectory = Path.Combine(output, culture);
            Directory.CreateDirectory(cultureDirectory);
            File.Copy(source, Path.Combine(cultureDirectory, resourceFile));
        });

        var manifest = NUnitGenerationTestEnvironment.CreateBuilder(
            NUnitGenerationTestEnvironment.CreateIsolatedGenerationsRoot()).Build(testAssembly);

        Assert.IsFalse(manifest.ManagedAssemblies.Any(path => path.EndsWith(resourceFile, StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(File.Exists(Path.Combine(manifest.ShadowDirectory, culture, resourceFile)));
    }

    [TestMethod]
    public void Native_files_copy_as_unmanaged()
    {
        using var workspace = new TempWorkspace();
        var testAssembly = NUnitGenerationTestEnvironment.CreateFixtureWorkspace(workspace.Root, "native", output =>
            File.WriteAllBytes(Path.Combine(output, "root.native.dll"), [0x4D, 0x5A, 0x90, 0x00]));

        var manifest = NUnitGenerationTestEnvironment.CreateBuilder(
            NUnitGenerationTestEnvironment.CreateIsolatedGenerationsRoot()).Build(testAssembly);

        Assert.IsTrue(File.Exists(Path.Combine(manifest.ShadowDirectory, "root.native.dll")));
        Assert.IsFalse(manifest.ManagedAssemblies.Any(path => path.EndsWith("root.native.dll", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(manifest.ManagedAssemblies.Any(path => path.EndsWith(NUnitGenerationTestEnvironment.FrameworkFileName, StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void Volatile_outputs_are_not_copied()
    {
        using var workspace = new TempWorkspace();
        var testAssembly = NUnitGenerationTestEnvironment.CreateFixtureWorkspace(workspace.Root, "volatile", output =>
        {
            Directory.CreateDirectory(Path.Combine(output, "Log"));
            File.WriteAllText(Path.Combine(output, "Log", "run.diag"), "volatile");
        });

        var manifest = NUnitGenerationTestEnvironment.CreateBuilder(
            NUnitGenerationTestEnvironment.CreateIsolatedGenerationsRoot()).Build(testAssembly);

        Assert.IsFalse(File.Exists(Path.Combine(manifest.ShadowDirectory, "Log", "run.diag")));
    }

    private sealed class TempWorkspace : IDisposable
    {
        public TempWorkspace()
        {
            Root = Path.Combine(Path.GetTempPath(), "DevTools.nunit." + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public void Dispose()
        {
            try { Directory.Delete(Root, true); }
            catch { }
        }
    }
}
