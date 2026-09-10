using System.Reflection;
using System.Runtime.CompilerServices;
using DevTools.AssemblyIsolation;
using DevTools.NUnit.Host.Tests.Loading;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Runtime;
using DevTools.Testing.Host.Loading;
using DevTools.Testing.Host.NUnit;
using DevTools.Testing.Host.NUnit.Loading;
using DevTools.Testing.Host.Runtime;
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
        var plan = NUnitRuntimeSessionFactory.CreateIsolationPlan(manifest, selectedFramework);

        Assert.NotSame(conflicting, selectedFramework);
        Assert.Same(selectedFramework, ResolveParent(plan, selectedFramework.GetName()));
        Assert.Same(
            typeof(ITestingRuntimeSession).Assembly,
            ResolveParent(plan, typeof(ITestingRuntimeSession).Assembly.GetName()));
        Assert.Same(
            typeof(TestRunRequest).Assembly,
            ResolveParent(plan, typeof(TestRunRequest).Assembly.GetName()));
        Assert.Equal(AssemblyIsolationKind.Isolated, plan.Kind);
        Assert.False(plan.LoadsFromDistinctFile);
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
        var plan = NUnitRuntimeSessionFactory.CreateIsolationPlan(manifest, framework);

        var privateName = AssemblyName.GetAssemblyName(NUnitRuntimeUnloadTestHelper.PrivateMicrosoftExtensionsStubPath);
        Assert.NotEqual(typeof(NullLogger).Assembly.GetName().Version, privateName.Version);
        Assert.False(plan.TryShare(privateName, out _));
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

        Assert.Throws<InvalidOperationException>(() => NUnitRuntimeSessionFactory.CreateIsolationPlan(ambiguousManaged, framework));

        var duplicateNative = NUnitRuntimeTestEnvironment.BuildGenerationWithDuplicateNativeAssets(workspace.Root);
        Assert.Throws<InvalidOperationException>(() => NUnitRuntimeSessionFactory.CreateIsolationPlan(duplicateNative, framework));
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

        Assert.Throws<ArgumentException>(() => NUnitRuntimeSessionFactory.CreateIsolationPlan(escapedManifest, framework));
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

        var session = Assert.IsType<IsolatedRuntimeSessionHandle>(factory.Create(manifest));
        var loadedTestAssembly = GetLoadedTestAssembly(session);
        var loadedRuntimeAssembly = session.RuntimeSession.GetType().Assembly;
        Assert.True(NUnitFrameworkHostShare.TryGetLoaded(out var loadedFrameworkAssembly));

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
            new TestRunRequest(
                1,
                runId,
                TestFrameworkId.NUnit,
                new TestAssemblyReference(manifest.ShadowAssemblyPath),
                TestSelection.FromFrameworkFilter(
                    NUnitSelectionFilter.XmlFilterFormat,
                    "<filter><test>DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.PlainTest_Passes</test></filter>")),
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
        Assert.True(plan.TryShare(identity, out var resolved));
        return resolved;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static DevTools.AssemblyIsolation.Diagnostics.AssemblyUnloadResult CreateDisposeAndVerifyUnload(
        TestingGenerationManifest manifest)
    {
        var session = (IsolatedRuntimeSessionHandle)new NUnitRuntimeSessionFactory().Create(manifest);
        session.Dispose();
        return session.VerifyUnload();
    }

    private static Assembly GetLoadedTestAssembly(IsolatedRuntimeSessionHandle session)
    {
        var field = session.RuntimeSession.GetType().GetField("_testAssembly", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Runtime session test assembly field not found.");
        return (Assembly)field.GetValue(session.RuntimeSession)!;
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
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }

    private sealed class RecordingSink : ITestingRuntimeEventSink
    {
        internal List<TestEvent> Events { get; } = [];

        public void Publish(TestEvent testingEvent) => Events.Add(testingEvent);
    }
}
