using System.Reflection;
using System.Runtime.CompilerServices;
using DevTools.AssemblyIsolation;
using DevTools.NUnit.Host.Loading;
using DevTools.NUnit.Host.Tests.Loading;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Runtime;
using DevTools.Testing.Host.Loading;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevTools.NUnit.Host.Tests;

public sealed class NUnitAssemblyIsolationTests
{
    [Fact]
    public void A_plan_uses_the_generation_selected_nunit_framework_and_neutral_contracts()
    {
        var manifest = NUnitRuntimeTestEnvironment.BuildFixtureGeneration();
        var conflicting = NUnitRuntimeUnloadTestHelper.LoadConflictingNUnitIntoDefaultContext();

        var selectedFramework = NUnitFrameworkHostShare.GetOrLoadFromShadow(
            NUnitGenerationPolicy.GetFrameworkAssemblyPath(manifest));
        var plan = NUnitIsolationPlan.Create(manifest, selectedFramework);

        Assert.NotSame(conflicting, selectedFramework);
        Assert.Same(selectedFramework, ResolveParent(plan, selectedFramework.GetName()));
        Assert.Same(
            typeof(ITestingRuntimeSession).Assembly,
            ResolveParent(plan, typeof(ITestingRuntimeSession).Assembly.GetName()));
        Assert.Same(
            typeof(TestingRunRequest).Assembly,
            ResolveParent(plan, typeof(TestingRunRequest).Assembly.GetName()));
        Assert.Equal(AssemblyIsolationLifecycle.Collectible, plan.Lifecycle);
    }

    [Fact]
    public void Plan_keeps_private_system_and_microsoft_dependencies_out_of_parent_bindings()
    {
        using var workspace = new TempWorkspace();
        var testAssembly = NUnitGenerationTestEnvironment.CreateFixtureWorkspace(
            workspace.Root,
            "private-platform-dependencies",
            outputDirectory => File.Copy(
                NUnitRuntimeUnloadTestHelper.PrivateMicrosoftExtensionsStubPath,
                Path.Combine(outputDirectory, "Microsoft.Extensions.Logging.Abstractions.dll"),
                overwrite: true));
        var manifest = NUnitRuntimeTestEnvironment.CreateBuilder(
                NUnitGenerationTestEnvironment.CreateIsolatedGenerationsRoot())
            .Build(testAssembly);
        var framework = NUnitFrameworkHostShare.GetOrLoadFromShadow(
            NUnitGenerationPolicy.GetFrameworkAssemblyPath(manifest));
        var plan = NUnitIsolationPlan.Create(manifest, framework);

        var privateName = AssemblyName.GetAssemblyName(NUnitRuntimeUnloadTestHelper.PrivateMicrosoftExtensionsStubPath);
        Assert.NotEqual(typeof(NullLogger).Assembly.GetName().Version, privateName.Version);
        Assert.False(plan.ParentBindings.TryResolve(privateName, out _));
        Assert.Contains(plan.ManagedSources, source => source.Resolve(privateName) is not null);
    }

    [Fact]
    public void Plan_rejects_ambiguous_managed_identities_and_native_assets()
    {
        using var workspace = new TempWorkspace();
        var manifest = NUnitRuntimeTestEnvironment.BuildFixtureGeneration();
        var conflictingFrameworkPath = Path.Combine(manifest.ShadowDirectory, "alternate", NUnitGenerationPolicy.FrameworkAssemblyFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(conflictingFrameworkPath)!);
        File.Copy(NUnitRuntimeUnloadTestHelper.ConflictingNUnitStubPath, conflictingFrameworkPath);
        var framework = NUnitFrameworkHostShare.GetOrLoadFromShadow(
            NUnitGenerationPolicy.GetFrameworkAssemblyPath(manifest));

        var ambiguousManaged = manifest with
        {
            ManagedAssemblies = manifest.ManagedAssemblies.Append(conflictingFrameworkPath).ToArray(),
        };

        Assert.Throws<InvalidOperationException>(() => NUnitIsolationPlan.Create(ambiguousManaged, framework));

        var duplicateNative = NUnitRuntimeTestEnvironment.BuildGenerationWithDuplicateNativeAssets(workspace.Root);
        Assert.Throws<InvalidOperationException>(() => NUnitIsolationPlan.Create(duplicateNative, framework));
    }

    [Fact]
    public void Plan_rejects_manifest_assets_outside_the_generation_shadow_directory()
    {
        using var workspace = new TempWorkspace();
        var manifest = NUnitRuntimeTestEnvironment.BuildFixtureGeneration();
        var externalAssemblyPath = Path.Combine(workspace.Root, "outside.dll");
        File.Copy(manifest.RuntimeAssemblyPath, externalAssemblyPath);
        var framework = NUnitFrameworkHostShare.GetOrLoadFromShadow(
            NUnitGenerationPolicy.GetFrameworkAssemblyPath(manifest));

        var escapedManifest = manifest with
        {
            ManagedAssemblies = manifest.ManagedAssemblies.Append(externalAssemblyPath).ToArray(),
        };

        Assert.Throws<ArgumentException>(() => NUnitIsolationPlan.Create(escapedManifest, framework));
    }

    [Fact]
    public void Runtime_session_preserves_contract_identity_and_source_unlock()
    {
        using var workspace = new TempWorkspace();
        var sourceAssembly = NUnitGenerationTestEnvironment.CreateGenerationOneAssembly(workspace.Root, "runtime-session");
        var manifest = NUnitRuntimeTestEnvironment.CreateBuilder(
                NUnitGenerationTestEnvironment.CreateIsolatedGenerationsRoot())
            .Build(sourceAssembly);
        var factory = new NUnitRuntimeSessionFactory();

        var session = Assert.IsType<NUnitRuntimeSessionHandle>(factory.Create(manifest));
        var loadedTestAssembly = session.GetLoadedTestAssembly();
        var loadedRuntimeAssembly = session.GetLoadedRuntimeAssembly();
        var loadedFrameworkAssembly = NUnitRuntimeSessionHandle.GetLoadedFrameworkAssembly();

        Assert.Same(NUnitFrameworkHostShare.GetOrLoadFromShadow(
            NUnitGenerationPolicy.GetFrameworkAssemblyPath(manifest)), loadedFrameworkAssembly);
        Assert.Contains(
            typeof(ITestingRuntimeSession),
            loadedRuntimeAssembly
                .GetType("DevTools.NUnit.Runtime.NUnitRuntimeSession", throwOnError: true)!
                .GetInterfaces());

        var sink = new RecordingSink();
        var runId = Guid.NewGuid();
        var response = session.Run(
            new TestingRunRequest(
                1,
                runId,
                "nunit",
                new TestingAssemblyReference(manifest.ShadowAssemblyPath, "net10.0-windows", null),
                new TestingSelection(
                    [],
                    "<filter><test>DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.PlainTest_Passes</test></filter>"),
                new Dictionary<string, string>()),
            sink,
            TestContext.Current.CancellationToken);

        var result = Assert.Single(response.Results);
        Assert.Equal(runId, response.RunId);
        Assert.Equal("DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.PlainTest_Passes", result.FullName);
        Assert.NotNull(result.Attachments);
        Assert.Equal(result, Assert.Single(sink.Events, testingEvent => testingEvent.Kind == "case").Case);

        using (var sourceStream = new FileStream(
                   manifest.SourceAssemblyPath,
                   FileMode.Open,
                   FileAccess.ReadWrite,
                   FileShare.ReadWrite | FileShare.Delete))
        {
            Assert.True(sourceStream.CanWrite);
        }
        Assert.Equal("DevTools.NUnit.Runtime.Fixtures", loadedTestAssembly.GetName().Name);

        session.Dispose();
    }

    [Fact]
    public void Runtime_session_unloads_after_its_proxy_is_cleared()
    {
        using var workspace = new TempWorkspace();
        var sourceAssembly = NUnitGenerationTestEnvironment.CreateGenerationOneAssembly(workspace.Root, "runtime-unload");
        var manifest = NUnitRuntimeTestEnvironment.CreateBuilder(
                NUnitGenerationTestEnvironment.CreateIsolatedGenerationsRoot())
            .Build(sourceAssembly);

        Assert.True(CreateDisposeAndVerifyUnload(manifest).IsUnloaded);
    }

    private static Assembly ResolveParent(AssemblyIsolationPlan plan, AssemblyName identity)
    {
        Assert.True(plan.ParentBindings.TryResolve(identity, out var resolved));
        return resolved;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static DevTools.AssemblyIsolation.Diagnostics.AssemblyUnloadResult CreateDisposeAndVerifyUnload(
        TestingGenerationManifest manifest)
    {
        var session = (NUnitRuntimeSessionHandle)new NUnitRuntimeSessionFactory().Create(manifest);
        session.Dispose();
        return session.VerifyUnload();
    }

    private sealed class TempWorkspace : IDisposable
    {
        public TempWorkspace()
        {
            Root = Path.Combine(Path.GetTempPath(), "DevTools", "NUnit", "IsolationTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }

    private sealed class RecordingSink : ITestingRuntimeEventSink
    {
        internal List<TestingRuntimeEvent> Events { get; } = [];

        public void Publish(TestingRuntimeEvent testingEvent) => Events.Add(testingEvent);
    }
}
