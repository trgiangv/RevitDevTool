using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Host.Loading;
using DevTools.Testing.Host.NUnit;

namespace DevTools.NUnit.Host.Tests;

[TestClass]
public sealed class NUnitGenerationPolicyTests
{
    [TestMethod]
    public void Plan_takes_the_runtime_and_framework_from_the_output_only()
    {
        using var workspace = new TempWorkspace();
        var testAssembly = NUnitGenerationTestEnvironment.CreateGenerationOneAssembly(workspace.Root, "plan");
        var output = Path.GetDirectoryName(testAssembly)!;

        var plan = new OutputGeneration(TestFrameworkId.NUnit).CreatePlan(testAssembly);

        Assert.AreEqual(TestFrameworkId.NUnit, plan.FrameworkId);
        Assert.AreEqual("DevTools.NUnit.Runtime.dll", plan.RuntimeAssemblyRelativePath);
        Assert.IsTrue(plan.Files.Any(file => string.Equals(
            file.RelativePath, NUnitGenerationTestEnvironment.FrameworkFileName, StringComparison.OrdinalIgnoreCase)));
        foreach (var file in plan.Files)
            StringAssert.StartsWith(file.SourcePath, output, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void Plan_rejects_a_missing_NUnit_framework_assembly()
    {
        using var workspace = new TempWorkspace();
        var missing = NUnitGenerationTestEnvironment.CreateFixtureWorkspace(workspace.Root, "missing", output =>
            File.Delete(Path.Combine(output, NUnitGenerationTestEnvironment.FrameworkFileName)));
        var harness = NUnitGenerationTestEnvironment.CreateBuilder(NUnitGenerationTestEnvironment.CreateIsolatedGenerationsRoot());

        Assert.ThrowsExactly<TestingGenerationBuildException>(() => harness.Build(missing));
    }

    [TestMethod]
    public void Output_without_a_manifest_stops_with_the_update_sentence()
    {
        using var workspace = new TempWorkspace();
        var testAssembly = NUnitGenerationTestEnvironment.CreateFixtureWorkspace(workspace.Root, "no-manifest", output =>
            File.Delete(Path.Combine(output, "DevTools.NUnit.Runtime.json")));

        var stop = Assert.ThrowsExactly<TestingRuntimeIncompatibleException>(() =>
            new OutputGeneration(TestFrameworkId.NUnit).CreatePlan(testAssembly));

        Assert.Contains("Update RevitDevTool.TestAdapter to the latest version.", stop.Message, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Policy_keeps_Microsoft_and_System_dependencies_generation_private()
    {
        using var workspace = new TempWorkspace();
        var testAssembly = NUnitGenerationTestEnvironment.CreateFixtureWorkspace(workspace.Root, "private", output =>
        {
            File.Copy(typeof(Microsoft.Extensions.Logging.Abstractions.NullLogger).Assembly.Location,
                Path.Combine(output, "Microsoft.Extensions.Logging.Abstractions.dll"), true);
            File.Copy(typeof(NUnitGenerationPolicyTests).Assembly.Location,
                Path.Combine(output, "System.Custom.dll"), true);
        });

        var manifest = NUnitGenerationTestEnvironment.CreateBuilder(
            NUnitGenerationTestEnvironment.CreateIsolatedGenerationsRoot()).Build(testAssembly);

        Assert.IsTrue(manifest.ManagedAssemblies.Any(path => path.EndsWith("Microsoft.Extensions.Logging.Abstractions.dll", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(manifest.ManagedAssemblies.Any(path => path.EndsWith("System.Custom.dll", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void Policy_excludes_the_neutral_contract_identity_even_when_renamed()
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
    public void Policy_keeps_satellite_resources_out_of_the_managed_identity_manifest()
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
    public void Policy_classifies_native_assets_and_excludes_volatile_outputs()
    {
        using var workspace = new TempWorkspace();
        var testAssembly = NUnitGenerationTestEnvironment.CreateFixtureWorkspace(workspace.Root, "native", output =>
        {
            File.WriteAllBytes(Path.Combine(output, "root.native.dll"), [0x4D, 0x5A, 0x90, 0x00]);
            Directory.CreateDirectory(Path.Combine(output, "Log"));
            File.WriteAllText(Path.Combine(output, "Log", "run.diag"), "volatile");
        });

        var manifest = NUnitGenerationTestEnvironment.CreateBuilder(
            NUnitGenerationTestEnvironment.CreateIsolatedGenerationsRoot()).Build(testAssembly);

        Assert.IsTrue(File.Exists(Path.Combine(manifest.ShadowDirectory, "root.native.dll")));
        Assert.IsFalse(manifest.ManagedAssemblies.Any(path => path.EndsWith("root.native.dll", StringComparison.OrdinalIgnoreCase)));
        Assert.IsFalse(File.Exists(Path.Combine(manifest.ShadowDirectory, "Log", "run.diag")));
        Assert.IsTrue(manifest.ManagedAssemblies.Any(path => path.EndsWith(NUnitGenerationTestEnvironment.FrameworkFileName, StringComparison.OrdinalIgnoreCase)));
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
