using DevTools.NUnit.Host.Loading;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Host.Loading;

namespace DevTools.NUnit.Host.Tests;

public sealed class NUnitGenerationPolicyTests
{
    [Fact]
    public void Policy_creates_a_neutral_plan_with_the_NUnit_runtime_and_framework()
    {
        using var workspace = new TempWorkspace();
        var testAssembly = NUnitGenerationTestEnvironment.CreateGenerationOneAssembly(workspace.Root, "plan");
        var policy = new NUnitGenerationPolicy(() => NUnitGenerationTestEnvironment.CreateRuntimeStub(workspace.Root));

        var plan = policy.CreatePlan(testAssembly);

        Assert.Equal("nunit", plan.FrameworkId);
        Assert.Equal(NUnitGenerationPolicy.RuntimeAssemblyFileName, plan.RuntimeAssemblyRelativePath);
        Assert.Contains(plan.Files, file => string.Equals(
            file.RelativePath, NUnitGenerationPolicy.FrameworkAssemblyFileName, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Policy_rejects_missing_or_duplicate_NUnit_framework_assemblies()
    {
        using var workspace = new TempWorkspace();
        var missing = NUnitGenerationTestEnvironment.CreateFixtureWorkspace(workspace.Root, "missing", output =>
            File.Delete(Path.Combine(output, NUnitGenerationPolicy.FrameworkAssemblyFileName)));
        var missingHarness = NUnitGenerationTestEnvironment.CreateBuilder(
            NUnitGenerationTestEnvironment.CreateIsolatedGenerationsRoot(), workspace.Root);
        Assert.Throws<NUnitGenerationBuildException>(() => missingHarness.Build(missing));

        var duplicate = NUnitGenerationTestEnvironment.CreateFixtureWorkspace(workspace.Root, "duplicate", output =>
        {
            var extra = Path.Combine(output, "extra");
            Directory.CreateDirectory(extra);
            File.Copy(Path.Combine(output, NUnitGenerationPolicy.FrameworkAssemblyFileName),
                Path.Combine(extra, NUnitGenerationPolicy.FrameworkAssemblyFileName));
        });
        var duplicateHarness = NUnitGenerationTestEnvironment.CreateBuilder(
            NUnitGenerationTestEnvironment.CreateIsolatedGenerationsRoot(), workspace.Root);
        var exception = Assert.Throws<NUnitGenerationBuildException>(() => duplicateHarness.Build(duplicate));
        Assert.Contains("found 2", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Policy_pins_the_supported_NUnit_file_version()
    {
        var frameworkPath = Path.Combine(NUnitGenerationTestEnvironment.FixtureOutputDirectory,
            NUnitGenerationPolicy.FrameworkAssemblyFileName);
        NUnitGenerationPolicy.ValidateNUnitFrameworkVersion(frameworkPath);

        var exception = Assert.Throws<NUnitGenerationBuildException>(() =>
            NUnitGenerationPolicy.ValidateNUnitFrameworkVersion(typeof(NUnitGenerationPolicyTests).Assembly.Location));
        Assert.Contains("4.6.1.0", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
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
            NUnitGenerationTestEnvironment.CreateIsolatedGenerationsRoot(), workspace.Root).Build(testAssembly);

        Assert.Contains(manifest.ManagedAssemblies,
            path => path.EndsWith("Microsoft.Extensions.Logging.Abstractions.dll", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(manifest.ManagedAssemblies,
            path => path.EndsWith("System.Custom.dll", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Policy_excludes_the_neutral_contract_identity_even_when_renamed()
    {
        using var workspace = new TempWorkspace();
        var testAssembly = NUnitGenerationTestEnvironment.CreateFixtureWorkspace(workspace.Root, "contract", output =>
            File.Copy(typeof(TestingRunRequest).Assembly.Location,
                Path.Combine(output, "PrivateTestingContract.dll"), true));

        var manifest = NUnitGenerationTestEnvironment.CreateBuilder(
            NUnitGenerationTestEnvironment.CreateIsolatedGenerationsRoot(), workspace.Root).Build(testAssembly);

        Assert.DoesNotContain(manifest.ManagedAssemblies,
            path => path.EndsWith("PrivateTestingContract.dll", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Policy_keeps_satellite_resources_out_of_the_managed_identity_manifest()
    {
        using var workspace = new TempWorkspace();
        const string culture = "fr";
        const string resourceFile = "Microsoft.Testing.Extensions.MSBuild.resources.dll";
        var source = Path.Combine(AppContext.BaseDirectory, culture, resourceFile);
        Assert.True(File.Exists(source), $"Satellite fixture was not found: {source}");
        var testAssembly = NUnitGenerationTestEnvironment.CreateFixtureWorkspace(workspace.Root, "satellite", output =>
        {
            var cultureDirectory = Path.Combine(output, culture);
            Directory.CreateDirectory(cultureDirectory);
            File.Copy(source, Path.Combine(cultureDirectory, resourceFile));
        });

        var manifest = NUnitGenerationTestEnvironment.CreateBuilder(
            NUnitGenerationTestEnvironment.CreateIsolatedGenerationsRoot(), workspace.Root).Build(testAssembly);

        Assert.DoesNotContain(manifest.ManagedAssemblies,
            path => path.EndsWith(resourceFile, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(manifest.OtherFiles,
            path => path.EndsWith(Path.Combine(culture, resourceFile), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
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
            NUnitGenerationTestEnvironment.CreateIsolatedGenerationsRoot(), workspace.Root).Build(testAssembly);

        Assert.Contains(manifest.NativeAssets,
            path => path.EndsWith("root.native.dll", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(manifest.OtherFiles,
            path => path.EndsWith("run.diag", StringComparison.OrdinalIgnoreCase));
        Assert.StartsWith(manifest.ShadowDirectory,
            NUnitGenerationPolicy.GetFrameworkAssemblyPath(manifest), StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TempWorkspace : IDisposable
    {
        public TempWorkspace()
        {
            Root = Path.Combine(Path.GetTempPath(), "DevTools", "NUnit", "PolicyTests", Guid.NewGuid().ToString("N"));
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
