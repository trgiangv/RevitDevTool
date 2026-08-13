using System.Reflection;
using System.Runtime.Loader;
using DevTools.NUnit.Core.Contracts;
using DevTools.NUnit.Core.Results;
using DevTools.NUnit.Core.Runtime;
using DevTools.NUnit.Host.Loading;
using DevTools.NUnit.Host.Tests.Loading;

namespace DevTools.NUnit.Host.Tests;

public sealed class NUnitRuntimeSessionFactoryTests
{
    [Fact]
    public void Create_executes_focused_fixture_case_and_reports_generation_metadata()
    {
        var manifest = NUnitRuntimeTestEnvironment.BuildFixtureGeneration();
        var factory = new NUnitRuntimeSessionFactory();

        using var session = factory.Create(manifest);
        Assert.Equal(manifest.GenerationId, session.GenerationId);

        var handle = Assert.IsType<NUnitRuntimeSessionHandle>(session);
        var generationFramework = handle.GetLoadedFrameworkAssembly();
        Assert.Equal(new Version(4, 6, 0, 0), generationFramework.GetName().Version);
        var frameworkAlc = AssemblyLoadContext.GetLoadContext(generationFramework);
        Assert.NotNull(frameworkAlc);
        Assert.False(frameworkAlc!.IsCollectible);

        var discoverAll = session.Discover(
            new NUnitDiscoverRequest(manifest.ShadowAssemblyPath, null));
        Assert.True(discoverAll.Cases.Count > 0);

        var discover = session.Discover(
            new NUnitDiscoverRequest(
                manifest.ShadowAssemblyPath,
                "<filter><test>DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.PlainTest_Passes</test></filter>"));

        Assert.Equal(manifest.GenerationId, discover.GenerationId);
        var discoveredCase = Assert.Single(discover.Cases);
        Assert.EndsWith("PlainTest_Passes", discoveredCase.FullName, StringComparison.Ordinal);

        var run = session.Run(
            new NUnitRunRequest(
                Guid.NewGuid(),
                manifest.ShadowAssemblyPath,
                "<filter><test>DevTools.NUnit.Runtime.Fixtures.FullSemanticsFixture.PlainTest_Passes</test></filter>"),
            new NoOpEventSink(),
            CancellationToken.None);

        Assert.Equal(manifest.GenerationId, run.GenerationId);
        var executedCase = Assert.Single(run.Cases);
        Assert.Equal(NUnitOutcomes.Passed, executedCase.Outcome);
        Assert.EndsWith("PlainTest_Passes", executedCase.Name, StringComparison.Ordinal);

        var runtimeAssembly = handle.GetLoadedRuntimeAssembly();
        var testAssembly = handle.GetLoadedTestAssembly();
        var generationAlc = AssemblyLoadContext.GetLoadContext(testAssembly);
        Assert.NotNull(generationAlc);
        Assert.True(generationAlc.IsCollectible);
        Assert.Same(generationAlc, AssemblyLoadContext.GetLoadContext(runtimeAssembly));
        Assert.NotSame(generationAlc, frameworkAlc);
    }

    [Fact]
    public void Create_leaves_shadow_runtime_and_test_assemblies_writable()
    {
        var manifest = NUnitRuntimeTestEnvironment.BuildFixtureGeneration();
        using var session = new NUnitRuntimeSessionFactory().Create(manifest);
        Assert.Equal(manifest.GenerationId, session.GenerationId);

        AssertFileWritable(manifest.ShadowAssemblyPath);
        AssertFileWritable(manifest.RuntimeAssemblyPath);
    }

    [Fact]
    public void Create_uses_host_shared_nunit_distinct_from_default_context_conflicting_copy()
    {
        var conflicting = NUnitRuntimeUnloadTestHelper.LoadConflictingNUnitIntoDefaultContext();
        Assert.Equal("nunit.framework", conflicting.GetName().Name, StringComparer.OrdinalIgnoreCase);
        Assert.NotEqual(
            new Version(4, 6, 1, 0),
            conflicting.GetName().Version);

        var manifest = NUnitRuntimeTestEnvironment.BuildFixtureGeneration();
        Assert.False(
            conflicting.Location.StartsWith(manifest.ShadowDirectory, StringComparison.OrdinalIgnoreCase));

        var factory = new NUnitRuntimeSessionFactory();
        using var session = factory.Create(manifest);
        var handle = Assert.IsType<NUnitRuntimeSessionHandle>(session);
        var generationFramework = handle.GetLoadedFrameworkAssembly();

        Assert.NotSame(conflicting, generationFramework);
        Assert.Equal(new Version(3, 14, 0, 0), conflicting.GetName().Version);
        Assert.Equal(new Version(4, 6, 0, 0), generationFramework.GetName().Version);
        var frameworkAlc = AssemblyLoadContext.GetLoadContext(generationFramework);
        Assert.NotNull(frameworkAlc);
        Assert.False(frameworkAlc!.IsCollectible);
        Assert.NotSame(
            AssemblyLoadContext.GetLoadContext(handle.GetLoadedTestAssembly()),
            frameworkAlc);
    }

    [Fact]
    public void ResolveAssembly_binds_system_console_from_default_context()
    {
        var manifest = NUnitRuntimeTestEnvironment.BuildFixtureGeneration();
        var loadContext = new NUnitRuntimeLoadContext(manifest);
        var requested = new AssemblyName("System.Console, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");

        var resolved = loadContext.ResolveAssemblyForTesting(requested);

        Assert.NotNull(resolved);
        Assert.Same(AssemblyLoadContext.Default, AssemblyLoadContext.GetLoadContext(resolved!));
    }

    [Fact]
    public void ResolveAssembly_binds_shared_core_from_default_context()
    {
        var manifest = NUnitRuntimeTestEnvironment.BuildFixtureGeneration();
        var hostCore = typeof(INUnitRuntimeSession).Assembly;
        var loadContext = new NUnitRuntimeLoadContext(manifest);

        var resolvedCore = loadContext.ResolveAssemblyForTesting(hostCore.GetName());
        Assert.NotNull(resolvedCore);

        Assert.Same(hostCore, resolvedCore);
        Assert.Same(AssemblyLoadContext.Default, AssemblyLoadContext.GetLoadContext(resolvedCore!));
    }

    [Fact]
    public void ResolveAssembly_leaves_unknown_dependency_to_normal_clr_binding()
    {
        var preloaded = NUnitRuntimeUnloadTestHelper.LoadGenerationPrivateDependencyIntoDefaultContext();
        var manifest = NUnitRuntimeTestEnvironment.BuildFixtureGeneration();

        Assert.False(NUnitSharedAssemblyPolicy.IsShared(preloaded.GetName().Name!));
        Assert.DoesNotContain(
            manifest.ManagedAssemblies,
            path => path.EndsWith("GenerationPrivateDependency.dll", StringComparison.OrdinalIgnoreCase));

        var loadContext = new NUnitRuntimeLoadContext(manifest);
        var resolved = loadContext.ResolveAssemblyForTesting(preloaded.GetName());

        Assert.Null(resolved);
    }

    [Fact]
    public void IsCompatibleIdentity_rejects_public_key_token_mismatch()
    {
        var candidate = new AssemblyName("Sample.Assembly")
        {
            Version = new Version(1, 0, 0, 0),
        };

        var requested = new AssemblyName("Sample.Assembly")
        {
            Version = new Version(1, 0, 0, 0),
        };
        requested.SetPublicKeyToken([0x01, 0x02, 0x03, 0x04]);

        Assert.False(NUnitGenerationManagedAssemblyIndex.IsCompatibleIdentity(requested, candidate));
    }

    [Fact]
    public void IsCompatibleIdentity_rejects_culture_mismatch()
    {
        var candidate = new AssemblyName("Sample.Assembly")
        {
            Version = new Version(1, 0, 0, 0),
            CultureInfo = new System.Globalization.CultureInfo("en-US"),
        };

        var requested = new AssemblyName("Sample.Assembly")
        {
            Version = new Version(1, 0, 0, 0),
            CultureInfo = new System.Globalization.CultureInfo("fr-FR"),
        };

        Assert.False(NUnitGenerationManagedAssemblyIndex.IsCompatibleIdentity(requested, candidate));
    }

    [Fact]
    public void IsCompatibleIdentity_rejects_satellite_when_request_is_neutral()
    {
        var candidate = new AssemblyName("Microsoft.TestPlatform.CommunicationUtilities.resources")
        {
            Version = new Version(17, 0, 0, 0),
            CultureInfo = new System.Globalization.CultureInfo("cs"),
        };

        var requested = new AssemblyName("Microsoft.TestPlatform.CommunicationUtilities.resources")
        {
            Version = new Version(17, 0, 0, 0),
        };

        Assert.False(NUnitGenerationManagedAssemblyIndex.IsCompatibleIdentity(requested, candidate));
    }

    [Fact]
    public void IsCompatibleIdentity_accepts_matching_satellite_culture()
    {
        var candidate = new AssemblyName("Microsoft.TestPlatform.CommunicationUtilities.resources")
        {
            Version = new Version(17, 0, 0, 0),
            CultureInfo = new System.Globalization.CultureInfo("de"),
        };

        var requested = new AssemblyName("Microsoft.TestPlatform.CommunicationUtilities.resources")
        {
            Version = new Version(17, 0, 0, 0),
            CultureInfo = new System.Globalization.CultureInfo("de"),
        };

        Assert.True(NUnitGenerationManagedAssemblyIndex.IsCompatibleIdentity(requested, candidate));
    }

    [Fact]
    public void NormalizeCulture_treats_neutral_as_empty()
    {
        Assert.Equal(string.Empty, NUnitGenerationManagedAssemblyIndex.NormalizeCulture(null));
        Assert.Equal(string.Empty, NUnitGenerationManagedAssemblyIndex.NormalizeCulture(string.Empty));
        Assert.Equal(string.Empty, NUnitGenerationManagedAssemblyIndex.NormalizeCulture("neutral"));
        Assert.Equal("cs", NUnitGenerationManagedAssemblyIndex.NormalizeCulture("cs"));
    }

    [Fact]
    public void ResolveAssembly_rejects_resolver_path_with_incompatible_identity()
    {
        var manifest = NUnitRuntimeTestEnvironment.BuildFixtureGeneration();
        var loadContext = new NUnitRuntimeLoadContext(manifest);
        var generationFramework = AssemblyName.GetAssemblyName(manifest.FrameworkAssemblyPath);

        var incompatibleRequest = new AssemblyName(generationFramework.Name!)
        {
            Version = new Version(9, 9, 9, 9),
        };

        var ex = Assert.Throws<NUnitGenerationAssemblyResolutionException>(
            () => loadContext.ResolveAssemblyForTesting(incompatibleRequest));

        Assert.Contains("incompatible", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveNativeAsset_rejects_ambiguous_duplicate_filenames_when_resolver_does_not_select()
    {
        using var workspace = new TempWorkspace();
        var manifest = NUnitRuntimeTestEnvironment.BuildGenerationWithDuplicateNativeAssets(workspace.Root);
        var loadContext = new NUnitRuntimeLoadContext(manifest);

        var ex = Assert.Throws<NUnitGenerationLoadException>(
            () => loadContext.ResolveNativeAssetForTesting("sample.native"));

        Assert.Contains("Ambiguous native asset", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveNativeAsset_returns_unique_manifest_path()
    {
        using var workspace = new TempWorkspace();
        var manifest = NUnitRuntimeTestEnvironment.BuildGenerationWithUniqueNativeAsset(workspace.Root);
        var loadContext = new NUnitRuntimeLoadContext(manifest);

        var resolved = loadContext.ResolveNativeAssetForTesting("sample.native.dll");

        var expected = manifest.NativeAssets.Single();
        Assert.Equal(expected, resolved, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveNativeAsset_accepts_resolver_selected_manifest_path()
    {
        using var workspace = new TempWorkspace();
        var manifest = NUnitRuntimeTestEnvironment.BuildGenerationWithDuplicateNativeAssets(workspace.Root);
        var expected = manifest.NativeAssets.Single(path =>
            path.Contains($"{Path.DirectorySeparatorChar}win-x64{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));

        var resolver = new AssemblyDependencyResolver(manifest.ShadowAssemblyPath);
        var resolverPath = resolver.ResolveUnmanagedDllToPath("sample.native");
        if (resolverPath is null)
        {
            // Resolver selection requires deps metadata; validate manifest-only ambiguity path instead.
            Assert.Throws<NUnitGenerationLoadException>(
                () => new NUnitRuntimeLoadContext(manifest).ResolveNativeAssetForTesting("sample.native"));
            return;
        }

        Assert.Equal(expected, Path.GetFullPath(resolverPath), StringComparer.OrdinalIgnoreCase);

        var loadContext = new NUnitRuntimeLoadContext(manifest);
        var resolved = loadContext.ResolveNativeAssetForTesting("sample.native");
        Assert.Equal(expected, resolved, StringComparer.OrdinalIgnoreCase);
    }

    private sealed class NoOpEventSink : INUnitRuntimeEventSink
    {
        public void Publish(NUnitRuntimeEvent runtimeEvent)
        {
        }
    }

    private static void AssertFileWritable(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.Read);
        stream.SetLength(stream.Length);
    }

    private sealed class TempWorkspace : IDisposable
    {
        public TempWorkspace()
        {
            Root = Path.Combine(
                Path.GetTempPath(),
                "DevTools",
                "NUnit",
                "RuntimeNativeTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                    Directory.Delete(Root, recursive: true);
            }
            catch
            {
                // Best-effort cleanup for temp workspaces.
            }
        }
    }
}
