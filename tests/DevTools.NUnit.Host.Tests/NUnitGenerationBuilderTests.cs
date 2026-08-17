using System.Collections.Concurrent;
using DevTools.NUnit.Core.Runtime;
using DevTools.NUnit.Host.Loading;
using DevTools.Utilities.AssemblyLoading;

namespace DevTools.NUnit.Host.Tests;

public sealed class NUnitGenerationBuilderTests
{
    [Fact]
    public void Build_creates_distinct_generations_for_same_name_and_version_with_different_il()
    {
        using var workspace = new TempWorkspace();
        var generationOne = NUnitGenerationTestEnvironment.CreateGenerationOneAssembly(
            workspace.Root,
            "generation-one");
        var generationTwo = NUnitGenerationTestEnvironment.CreateGenerationTwoAssembly(
            workspace.Root,
            "generation-two");
        var generationsRoot = NUnitGenerationTestEnvironment.CreateIsolatedGenerationsRoot();
        var builder = NUnitGenerationTestEnvironment.CreateBuilder(generationsRoot, workspace.Root);

        var manifestOne = builder.Build(generationOne);
        var manifestTwo = builder.Build(generationTwo);

        Assert.NotEqual(manifestOne.GenerationId, manifestTwo.GenerationId);
        Assert.NotEqual(manifestOne.ShadowDirectory, manifestTwo.ShadowDirectory);
        Assert.True(Directory.Exists(manifestOne.ShadowDirectory));
        Assert.True(Directory.Exists(manifestTwo.ShadowDirectory));
        Assert.NotEqual(
            Convert.ToHexString(File.ReadAllBytes(manifestOne.ShadowAssemblyPath)),
            Convert.ToHexString(File.ReadAllBytes(manifestTwo.ShadowAssemblyPath)));
    }

    [Fact]
    public void Build_leaves_source_dll_and_pdb_writable_after_shadow_load()
    {
        using var workspace = new TempWorkspace();
        var testAssembly = NUnitGenerationTestEnvironment.CreateGenerationOneAssembly(
            workspace.Root,
            "writable-after-load");
        var testPdb = Path.ChangeExtension(testAssembly, ".pdb");
        Assert.True(File.Exists(testPdb));

        var generationsRoot = NUnitGenerationTestEnvironment.CreateIsolatedGenerationsRoot();
        var builder = NUnitGenerationTestEnvironment.CreateBuilder(generationsRoot, workspace.Root);
        var manifest = builder.Build(testAssembly);

        var loadedShadowAssembly = NUnitGenerationShadowLoadTestHelper.LoadShadowAssembly(manifest.ShadowAssemblyPath);

        Assert.NotEqual(
            Path.GetFullPath(testAssembly),
            Path.GetFullPath(loadedShadowAssembly.Location));
        Assert.StartsWith(
            Path.GetFullPath(manifest.ShadowDirectory),
            Path.GetFullPath(loadedShadowAssembly.Location),
            StringComparison.OrdinalIgnoreCase);

        NUnitGenerationShadowLoadTestHelper.AssertSourceOutputsRemainWritable(testAssembly, testPdb);

        _ = loadedShadowAssembly;
    }

    [Fact]
    public void Build_preserves_native_relative_paths()
    {
        using var workspace = new TempWorkspace();
        var testAssembly = NUnitGenerationTestEnvironment.CreateFixtureWorkspace(
            workspace.Root,
            "native",
            outputDirectory =>
            {
                var nativeDirectory = Path.Combine(outputDirectory, "runtimes", "win-x64", "native");
                Directory.CreateDirectory(nativeDirectory);
                File.WriteAllBytes(
                    Path.Combine(nativeDirectory, "sample.native.dll"),
                    [0x4D, 0x5A, 0x90, 0x00]);
            });

        var generationsRoot = NUnitGenerationTestEnvironment.CreateIsolatedGenerationsRoot();
        var builder = NUnitGenerationTestEnvironment.CreateBuilder(generationsRoot, workspace.Root);
        var manifest = builder.Build(testAssembly);

        var expectedRelative = Path.Combine("runtimes", "win-x64", "native", "sample.native.dll");
        var expectedShadowPath = Path.Combine(manifest.ShadowDirectory, expectedRelative);
        Assert.Contains(expectedShadowPath, manifest.NativeAssets);
        Assert.True(File.Exists(expectedShadowPath));
    }

    [Fact]
    public void Build_fails_when_nunit_framework_is_missing()
    {
        using var workspace = new TempWorkspace();
        var testAssembly = NUnitGenerationTestEnvironment.CreateFixtureWorkspace(
            workspace.Root,
            "missing-nunit",
            outputDirectory =>
            {
                File.Delete(Path.Combine(outputDirectory, NUnitGenerationBuilder.FrameworkAssemblyFileName));
            });

        var generationsRoot = NUnitGenerationTestEnvironment.CreateIsolatedGenerationsRoot();
        var builder = NUnitGenerationTestEnvironment.CreateBuilder(generationsRoot, workspace.Root);

        var ex = Assert.Throws<NUnitGenerationBuildException>(() => builder.Build(testAssembly));
        Assert.Contains("nunit.framework.dll", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_fails_when_multiple_nunit_framework_assemblies_exist()
    {
        using var workspace = new TempWorkspace();
        var testAssembly = NUnitGenerationTestEnvironment.CreateFixtureWorkspace(
            workspace.Root,
            "duplicate-nunit",
            outputDirectory =>
            {
                var frameworkPath = Path.Combine(outputDirectory, NUnitGenerationBuilder.FrameworkAssemblyFileName);
                var duplicateDirectory = Path.Combine(outputDirectory, "extra");
                Directory.CreateDirectory(duplicateDirectory);
                File.Copy(
                    frameworkPath,
                    Path.Combine(duplicateDirectory, NUnitGenerationBuilder.FrameworkAssemblyFileName),
                    overwrite: true);
            });

        var generationsRoot = NUnitGenerationTestEnvironment.CreateIsolatedGenerationsRoot();
        var builder = NUnitGenerationTestEnvironment.CreateBuilder(generationsRoot, workspace.Root);

        var ex = Assert.Throws<NUnitGenerationBuildException>(() => builder.Build(testAssembly));
        Assert.Contains("found 2", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateNUnitFrameworkVersion_accepts_assembly_version_4600_with_file_version_4610()
    {
        var frameworkPath = Path.Combine(
            NUnitGenerationTestEnvironment.FixtureOutputDirectory,
            NUnitGenerationBuilder.FrameworkAssemblyFileName);

        var exception = Record.Exception(
            () => NUnitGenerationBuilder.ValidateNUnitFrameworkVersion(frameworkPath));

        Assert.Null(exception);
    }

    [Fact]
    public void ValidateNUnitFrameworkVersion_rejects_mismatched_file_version()
    {
        var ex = Assert.Throws<NUnitGenerationBuildException>(
            () => NUnitGenerationBuilder.ValidateNUnitFrameworkVersion(
                typeof(NUnitGenerationBuilderTests).Assembly.Location));

        Assert.Contains("4.6.1.0", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_is_deterministic_for_identical_content()
    {
        using var workspace = new TempWorkspace();
        var testAssembly = NUnitGenerationTestEnvironment.CreateGenerationOneAssembly(
            workspace.Root,
            "deterministic");
        var generationsRoot = NUnitGenerationTestEnvironment.CreateIsolatedGenerationsRoot();
        var builder = NUnitGenerationTestEnvironment.CreateBuilder(generationsRoot, workspace.Root);

        var first = builder.Build(testAssembly);
        var second = builder.Build(testAssembly);

        Assert.Equal(first.GenerationId, second.GenerationId);
        Assert.Equal(first.ShadowDirectory, second.ShadowDirectory);
        Assert.Equal(
            File.ReadAllBytes(first.ShadowAssemblyPath),
            File.ReadAllBytes(second.ShadowAssemblyPath));
    }

    [Fact]
    public void Build_concurrent_calls_are_idempotent()
    {
        using var workspace = new TempWorkspace();
        var testAssembly = NUnitGenerationTestEnvironment.CreateGenerationOneAssembly(
            workspace.Root,
            "concurrent");
        var generationsRoot = NUnitGenerationTestEnvironment.CreateIsolatedGenerationsRoot();
        var builder = NUnitGenerationTestEnvironment.CreateBuilder(generationsRoot, workspace.Root);
        var manifests = new ConcurrentBag<NUnitGenerationManifest>();

        Parallel.For(0, 16, _ => manifests.Add(builder.Build(testAssembly)));

        var distinctIds = manifests.Select(static manifest => manifest.GenerationId).Distinct().ToList();
        var distinctDirectories = manifests.Select(static manifest => manifest.ShadowDirectory).Distinct().ToList();

        Assert.Single(distinctIds);
        Assert.Single(distinctDirectories);
        Assert.Equal(16, manifests.Count);
        Assert.True(File.Exists(Path.Combine(distinctDirectories[0], NUnitGenerationBuilder.GenerationCompleteMarkerFileName)));
    }

    [Fact]
    public void Build_does_not_copy_explicitly_shared_assemblies()
    {
        using var workspace = new TempWorkspace();
        var testAssembly = NUnitGenerationTestEnvironment.CreateFixtureWorkspace(
            workspace.Root,
            "shared",
            outputDirectory =>
            {
                File.Copy(
                    NUnitGenerationTestEnvironment.CoreAssemblyPath,
                    Path.Combine(outputDirectory, "DevTools.NUnit.Core.dll"),
                    overwrite: true);
            });

        var generationsRoot = NUnitGenerationTestEnvironment.CreateIsolatedGenerationsRoot();
        var builder = NUnitGenerationTestEnvironment.CreateBuilder(generationsRoot, workspace.Root);
        var manifest = builder.Build(testAssembly);

        Assert.False(NUnitSharedAssemblyPolicy.IsShared("nunit.framework"));
        Assert.True(NUnitSharedAssemblyPolicy.IsShared("DevTools.NUnit.Core"));
        Assert.False(File.Exists(Path.Combine(manifest.ShadowDirectory, "DevTools.NUnit.Core.dll")));
        Assert.DoesNotContain(
            manifest.ManagedAssemblies,
            path => path.EndsWith("DevTools.NUnit.Core.dll", StringComparison.OrdinalIgnoreCase));
        Assert.True(File.Exists(manifest.FrameworkAssemblyPath));
    }

    [Fact]
    public void Build_keeps_package_dependency_with_Microsoft_prefix_generation_private()
    {
        using var workspace = new TempWorkspace();
        var testAssembly = NUnitGenerationTestEnvironment.CreateFixtureWorkspace(
            workspace.Root,
            "private-deps",
            outputDirectory =>
            {
                var privateDependency = typeof(Microsoft.Extensions.Logging.Abstractions.NullLogger)
                    .Assembly
                    .Location;
                File.Copy(
                    privateDependency,
                    Path.Combine(outputDirectory, Path.GetFileName(privateDependency)),
                    overwrite: true);
            });

        var generationsRoot = NUnitGenerationTestEnvironment.CreateIsolatedGenerationsRoot();
        var builder = NUnitGenerationTestEnvironment.CreateBuilder(generationsRoot, workspace.Root);
        var manifest = builder.Build(testAssembly);

        Assert.False(NUnitSharedAssemblyPolicy.IsShared("Microsoft.Extensions.Logging.Abstractions"));

        var shadowDependency = Path.Combine(
            manifest.ShadowDirectory,
            "Microsoft.Extensions.Logging.Abstractions.dll");

        Assert.True(File.Exists(shadowDependency));
        Assert.Contains(shadowDependency, manifest.ManagedAssemblies);
    }

    [Fact]
    public void Build_excludes_shared_runtime_framework_dependencies_from_generation_copy()
    {
        using var workspace = new TempWorkspace();
        var testAssembly = NUnitGenerationTestEnvironment.CreateFixtureWorkspace(
            workspace.Root,
            "runtime-closure",
            outputDirectory =>
            {
                File.Delete(Path.Combine(outputDirectory, "System.Reflection.Metadata.dll"));
                File.Delete(Path.Combine(outputDirectory, "System.Collections.Immutable.dll"));
            });

        var runtimeSource = NUnitGenerationTestEnvironment.CreateRuntimeStub(workspace.Root);
        var runtimeDirectory = Path.GetDirectoryName(runtimeSource.AssemblyPath)!;
        File.Copy(
            typeof(System.Reflection.Metadata.MetadataReader).Assembly.Location,
            Path.Combine(runtimeDirectory, "System.Reflection.Metadata.dll"),
            overwrite: true);
        File.Copy(
            typeof(System.Collections.Immutable.ImmutableArray).Assembly.Location,
            Path.Combine(runtimeDirectory, "System.Collections.Immutable.dll"),
            overwrite: true);

        var builder = new NUnitGenerationBuilder(
            () => runtimeSource with
            {
                DependencyPaths = new[]
                {
                    Path.Combine(runtimeDirectory, "System.Reflection.Metadata.dll"),
                    Path.Combine(runtimeDirectory, "System.Collections.Immutable.dll"),
                },
            },
            NUnitGenerationTestEnvironment.CreateIsolatedGenerationsRoot());

        var manifest = builder.Build(testAssembly);

        Assert.DoesNotContain(
            manifest.ManagedAssemblies,
            path => path.EndsWith("System.Reflection.Metadata.dll", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            manifest.ManagedAssemblies,
            path => path.EndsWith("System.Collections.Immutable.dll", StringComparison.OrdinalIgnoreCase));
        Assert.False(File.Exists(Path.Combine(manifest.ShadowDirectory, "System.Reflection.Metadata.dll")));
        Assert.False(File.Exists(Path.Combine(manifest.ShadowDirectory, "System.Collections.Immutable.dll")));
    }

    [Fact]
    public void ComputeGenerationId_uses_portable_framing_order_and_path_normalization()
    {
        using var workspace = new TempWorkspace();
        var fileOne = Path.Combine(workspace.Root, "alpha.bin");
        var fileTwo = Path.Combine(workspace.Root, "nested", "beta.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(fileTwo)!);
        File.WriteAllBytes(fileOne, [0x01, 0x02, 0x03]);
        File.WriteAllBytes(fileTwo, [0xAA, 0xBB]);

        var entries = new List<(string RelativePath, string AbsolutePath)>
        {
            ("nested/beta.bin", fileTwo),
            ("alpha.bin", fileOne),
        };

        var first = NUnitGenerationContentHash.ComputeGenerationId(entries);
        var second = NUnitGenerationContentHash.ComputeGenerationId(
            entries.Select(entry => (NUnitGenerationBuilder.NormalizeRelativePath(entry.RelativePath), entry.AbsolutePath)).ToList());

        Assert.Equal(first, second);
        Assert.Matches("^[0-9a-f]{64}$", first);
    }

    [Fact]
    public void ComputeGenerationId_normalizes_windows_path_case_and_slashes_for_hashing()
    {
        using var workspace = new TempWorkspace();
        var betaPath = Path.Combine(workspace.Root, "Nested", "Beta.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(betaPath)!);
        File.WriteAllBytes(betaPath, [0x10, 0x20, 0x30]);

        var forwardSlash = NUnitGenerationContentHash.ComputeGenerationId([
            ("Nested/Beta.bin", betaPath),
        ]);
        var lowerCase = NUnitGenerationContentHash.ComputeGenerationId([
            ("nested/beta.bin", betaPath),
        ]);
        var backslash = NUnitGenerationContentHash.ComputeGenerationId([
            (@"nested\beta.bin", betaPath),
        ]);

        Assert.Equal(forwardSlash, lowerCase);
        Assert.Equal(lowerCase, backslash);
    }

    [Fact]
    public void Build_fails_when_published_generation_is_corrupted()
    {
        using var workspace = new TempWorkspace();
        var testAssembly = NUnitGenerationTestEnvironment.CreateGenerationOneAssembly(
            workspace.Root,
            "corrupted");
        var generationsRoot = NUnitGenerationTestEnvironment.CreateIsolatedGenerationsRoot();
        var builder = NUnitGenerationTestEnvironment.CreateBuilder(generationsRoot, workspace.Root);

        var manifest = builder.Build(testAssembly);
        var tamperedPath = manifest.ShadowAssemblyPath;
        File.WriteAllBytes(tamperedPath, [0x00, 0x01, 0x02, 0x03]);

        var ex = Assert.Throws<NUnitGenerationCorruptionException>(() => builder.Build(testAssembly));

        Assert.Equal(manifest.ShadowDirectory, ex.ShadowDirectory);
        Assert.Equal(manifest.GenerationId, ex.ExpectedGenerationId);
        Assert.NotEqual(ex.ExpectedGenerationId, ex.ActualGenerationId);
    }

    [Fact]
    public void Build_excludes_shared_assembly_identity_even_when_renamed_on_disk()
    {
        using var workspace = new TempWorkspace();
        var testAssembly = NUnitGenerationTestEnvironment.CreateFixtureWorkspace(
            workspace.Root,
            "shared-renamed",
            outputDirectory =>
            {
                File.Copy(
                    NUnitGenerationTestEnvironment.CoreAssemblyPath,
                    Path.Combine(outputDirectory, "PrivateDependency.dll"),
                    overwrite: true);
            });

        var generationsRoot = NUnitGenerationTestEnvironment.CreateIsolatedGenerationsRoot();
        var builder = NUnitGenerationTestEnvironment.CreateBuilder(generationsRoot, workspace.Root);
        var manifest = builder.Build(testAssembly);

        Assert.False(File.Exists(Path.Combine(manifest.ShadowDirectory, "PrivateDependency.dll")));

        var renamedCorePath = Path.Combine(workspace.Root, "shared-renamed", "PrivateDependency.dll");
        Assert.True(NUnitSharedAssemblyPolicy.ShouldExcludeFromGenerationCopy(renamedCorePath));
    }

    [Fact]
    public void Build_accepts_exe_test_assembly_and_skips_diagnostic_logs()
    {
        using var workspace = new TempWorkspace();
        var dll = NUnitGenerationTestEnvironment.CreateFixtureWorkspace(
            workspace.Root,
            "mtp-exe",
            outputDirectory =>
            {
                var logDirectory = Path.Combine(outputDirectory, "Log");
                Directory.CreateDirectory(logDirectory);
                File.WriteAllText(Path.Combine(logDirectory, "run.diag"), "volatile");
                var resultsDirectory = Path.Combine(outputDirectory, "TestResults");
                Directory.CreateDirectory(resultsDirectory);
                File.WriteAllText(Path.Combine(resultsDirectory, "out.txt"), "skip");
            });

        var exe = Path.Combine(Path.GetDirectoryName(dll)!, "DevTools.NUnit.SampleTests.exe");
        File.Copy(dll, exe, overwrite: true);

        var generationsRoot = NUnitGenerationTestEnvironment.CreateIsolatedGenerationsRoot();
        var builder = NUnitGenerationTestEnvironment.CreateBuilder(generationsRoot, workspace.Root);
        var manifest = builder.Build(exe);

        Assert.Equal(".exe", Path.GetExtension(manifest.ShadowAssemblyPath));
        Assert.True(File.Exists(manifest.ShadowAssemblyPath));
        Assert.Contains(manifest.ShadowAssemblyPath, manifest.ManagedAssemblies);
        Assert.False(Directory.Exists(Path.Combine(manifest.ShadowDirectory, "Log")));
        Assert.False(Directory.Exists(Path.Combine(manifest.ShadowDirectory, "TestResults")));
    }

    [Fact]
    public void Build_includes_private_assembly_even_when_renamed_to_shared_filename()
    {
        using var workspace = new TempWorkspace();
        var privateAssembly = typeof(NUnitGenerationBuilderTests).Assembly.Location;
        var testAssembly = NUnitGenerationTestEnvironment.CreateFixtureWorkspace(
            workspace.Root,
            "private-shared-name",
            outputDirectory =>
            {
                File.Copy(
                    privateAssembly,
                    Path.Combine(outputDirectory, "DevTools.NUnit.Core.dll"),
                    overwrite: true);
            });

        var generationsRoot = NUnitGenerationTestEnvironment.CreateIsolatedGenerationsRoot();
        var builder = NUnitGenerationTestEnvironment.CreateBuilder(generationsRoot, workspace.Root);
        var manifest = builder.Build(testAssembly);

        var shadowPath = Path.Combine(manifest.ShadowDirectory, "DevTools.NUnit.Core.dll");
        Assert.True(File.Exists(shadowPath));
        Assert.Contains(shadowPath, manifest.ManagedAssemblies);
        Assert.False(NUnitSharedAssemblyPolicy.IsShared(
            System.Reflection.AssemblyName.GetAssemblyName(privateAssembly).Name!));
    }

    [Fact]
    public void Build_retries_when_source_mutates_during_snapshot_copy_without_publishing_invalid_generation()
    {
        using var workspace = new TempWorkspace();
        var testAssembly = NUnitGenerationTestEnvironment.CreateGenerationOneAssembly(
            workspace.Root,
            "snapshot-copy");
        var generationsRoot = NUnitGenerationTestEnvironment.CreateIsolatedGenerationsRoot();
        var builder = NUnitGenerationTestEnvironment.CreateBuilder(generationsRoot, workspace.Root);

        builder.SnapshotCopyProgressHook = (sourcePath, phase) =>
        {
            if (phase != SnapshotCopyPhase.BeforeCopy
                || !string.Equals(sourcePath, testAssembly, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var bytes = File.ReadAllBytes(sourcePath);
            var mutated = new byte[bytes.Length + 1];
            bytes.CopyTo(mutated, 0);
            mutated[^1] = 0xAB;
            File.WriteAllBytes(sourcePath, mutated);
        };

        var ex = Assert.Throws<NUnitGenerationBuildException>(() => builder.Build(testAssembly));

        Assert.Contains("coherent generation snapshot", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            Directory.EnumerateFiles(generationsRoot, "*", SearchOption.AllDirectories),
            path => path.EndsWith(NUnitGenerationBuilder.GenerationCompleteMarkerFileName, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Build_does_not_publish_generation_when_source_mutates_after_snapshot()
    {
        using var workspace = new TempWorkspace();
        var testAssembly = NUnitGenerationTestEnvironment.CreateGenerationOneAssembly(
            workspace.Root,
            "toctou");
        var generationsRoot = NUnitGenerationTestEnvironment.CreateIsolatedGenerationsRoot();
        var builder = NUnitGenerationTestEnvironment.CreateBuilder(generationsRoot, workspace.Root);
        var originalBytes = File.ReadAllBytes(testAssembly);

        builder.AfterSnapshotBeforePublishHook = () =>
        {
            var mutated = new byte[originalBytes.Length + 1];
            originalBytes.CopyTo(mutated, 0);
            mutated[^1] = 0xFF;
            File.WriteAllBytes(testAssembly, mutated);
        };

        var manifest = builder.Build(testAssembly);

        Assert.Equal(originalBytes, File.ReadAllBytes(manifest.ShadowAssemblyPath));
        Assert.NotEqual(File.ReadAllBytes(testAssembly), File.ReadAllBytes(manifest.ShadowAssemblyPath));

        var publishedContentPaths = Directory.EnumerateFiles(manifest.ShadowDirectory, "*", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith(NUnitGenerationBuilder.GenerationCompleteMarkerFileName, StringComparison.OrdinalIgnoreCase))
            .Select(path => (RelativePath: NUnitGenerationBuilder.NormalizeRelativePath(Path.GetRelativePath(manifest.ShadowDirectory, path)), AbsolutePath: path))
            .ToList();

        Assert.Equal(manifest.GenerationId, NUnitGenerationContentHash.ComputeGenerationId(publishedContentPaths));
        Assert.All(
            Directory.GetDirectories(generationsRoot),
            directory => Assert.True(VerifyPublishedDirectoryMatchesId(directory)));
    }

    [Fact]
    public void Build_returns_published_manifest_for_unchanged_source()
    {
        using var workspace = new TempWorkspace();
        var testAssembly = NUnitGenerationTestEnvironment.CreateGenerationOneAssembly(
            workspace.Root,
            "published");
        var generationsRoot = NUnitGenerationTestEnvironment.CreateIsolatedGenerationsRoot();
        var builder = NUnitGenerationTestEnvironment.CreateBuilder(generationsRoot, workspace.Root);

        var first = builder.Build(testAssembly);
        var second = builder.Build(testAssembly);

        Assert.Equal(first.GenerationId, second.GenerationId);
        Assert.Equal(first.ShadowDirectory, second.ShadowDirectory);
        Assert.Equal(File.ReadAllBytes(first.ShadowAssemblyPath), File.ReadAllBytes(second.ShadowAssemblyPath));
    }

    [Fact]
    public void Build_creates_new_generation_when_source_content_changes()
    {
        using var workspace = new TempWorkspace();
        var testAssembly = NUnitGenerationTestEnvironment.CreateGenerationOneAssembly(
            workspace.Root,
            "changed");
        var generationsRoot = NUnitGenerationTestEnvironment.CreateIsolatedGenerationsRoot();
        var builder = NUnitGenerationTestEnvironment.CreateBuilder(generationsRoot, workspace.Root);

        var first = builder.Build(testAssembly);
        NUnitGenerationTestEnvironment.PatchGenerationMarker(testAssembly, "generation-two");
        var second = builder.Build(testAssembly);

        Assert.NotEqual(first.GenerationId, second.GenerationId);
        Assert.NotEqual(first.ShadowDirectory, second.ShadowDirectory);
    }

    [Fact]
    public void SharedAssemblyPolicy_shares_host_packages_and_system_prefix_not_microsoft_extensions()
    {
        HostSharedAssemblies.Use(new HostSharedAssemblyNames(["RevitAPI"], ["Autodesk."]));
        Assert.True(NUnitSharedAssemblyPolicy.IsShared("System"));
        Assert.True(NUnitSharedAssemblyPolicy.IsShared("System.Private.CoreLib"));
        Assert.True(NUnitSharedAssemblyPolicy.IsShared("System.Runtime"));
        Assert.True(NUnitSharedAssemblyPolicy.IsShared("System.Custom"));
        Assert.True(NUnitSharedAssemblyPolicy.IsShared("System.Reflection.Metadata"));
        Assert.True(NUnitSharedAssemblyPolicy.IsShared("System.Collections.Immutable"));
        Assert.True(NUnitSharedAssemblyPolicy.IsShared("Microsoft.Win32.Registry"));
        Assert.True(NUnitSharedAssemblyPolicy.IsShared("RevitAPI"));
        Assert.True(NUnitSharedAssemblyPolicy.IsShared("MahApps.Metro"));
        Assert.True(NUnitSharedAssemblyPolicy.IsShared("Autodesk.Revit.DB"));
        Assert.True(NUnitSharedAssemblyPolicy.IsShared(typeof(INUnitRuntimeSession).Assembly.GetName().Name!));
        Assert.False(NUnitSharedAssemblyPolicy.IsShared("Microsoft.Extensions.Logging.Abstractions"));
        Assert.False(NUnitSharedAssemblyPolicy.IsShared("ThirdParty.Custom"));
        Assert.False(NUnitSharedAssemblyPolicy.IsShared("JetBrains.Annotations"));
        Assert.False(NUnitSharedAssemblyPolicy.IsShared("nunit.framework"));
    }

    [Fact]
    public void Build_classifies_shared_prefixes_by_assembly_identity_not_filename()
    {
        using var workspace = new TempWorkspace();
        var testAssembly = NUnitGenerationTestEnvironment.CreateFixtureWorkspace(
            workspace.Root,
            "system-custom",
            outputDirectory =>
            {
                File.Copy(
                    typeof(NUnitGenerationBuilderTests).Assembly.Location,
                    Path.Combine(outputDirectory, "System.Custom.dll"),
                    overwrite: true);
            });

        var generationsRoot = NUnitGenerationTestEnvironment.CreateIsolatedGenerationsRoot();
        var builder = NUnitGenerationTestEnvironment.CreateBuilder(generationsRoot, workspace.Root);
        var manifest = builder.Build(testAssembly);

        var shadowPath = Path.Combine(manifest.ShadowDirectory, "System.Custom.dll");
        Assert.True(File.Exists(shadowPath));
        Assert.Contains(shadowPath, manifest.ManagedAssemblies);
    }

    [Fact]
    public void Build_classifies_root_native_dll_as_native_asset()
    {
        using var workspace = new TempWorkspace();
        var testAssembly = NUnitGenerationTestEnvironment.CreateFixtureWorkspace(
            workspace.Root,
            "root-native",
            outputDirectory =>
            {
                File.WriteAllBytes(
                    Path.Combine(outputDirectory, "root.native.dll"),
                    [0x4D, 0x5A, 0x90, 0x00]);
            });

        var generationsRoot = NUnitGenerationTestEnvironment.CreateIsolatedGenerationsRoot();
        var builder = NUnitGenerationTestEnvironment.CreateBuilder(generationsRoot, workspace.Root);
        var manifest = builder.Build(testAssembly);

        var expectedShadowPath = Path.Combine(manifest.ShadowDirectory, "root.native.dll");
        Assert.Contains(expectedShadowPath, manifest.NativeAssets);
        Assert.True(File.Exists(expectedShadowPath));
    }

    private static bool VerifyPublishedDirectoryMatchesId(string directory)
    {
        if (!File.Exists(Path.Combine(directory, NUnitGenerationBuilder.GenerationCompleteMarkerFileName)))
            return true;

        var generationId = Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var contentPaths = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith(NUnitGenerationBuilder.GenerationCompleteMarkerFileName, StringComparison.OrdinalIgnoreCase))
            .Select(path => (RelativePath: NUnitGenerationBuilder.NormalizeRelativePath(Path.GetRelativePath(directory, path)), AbsolutePath: path))
            .ToList();

        return string.Equals(
            NUnitGenerationContentHash.ComputeGenerationId(contentPaths),
            generationId,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Build_manifest_lists_one_coherent_shadow_directory()
    {
        using var workspace = new TempWorkspace();
        var testAssembly = NUnitGenerationTestEnvironment.CreateGenerationOneAssembly(
            workspace.Root,
            "manifest");
        var generationsRoot = NUnitGenerationTestEnvironment.CreateIsolatedGenerationsRoot();
        var builder = NUnitGenerationTestEnvironment.CreateBuilder(generationsRoot, workspace.Root);
        var manifest = builder.Build(testAssembly);

        Assert.Equal(
            Path.Combine(generationsRoot, manifest.GenerationId),
            manifest.ShadowDirectory);
        Assert.StartsWith(manifest.ShadowDirectory, manifest.ShadowAssemblyPath, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(manifest.ShadowDirectory, manifest.RuntimeAssemblyPath, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(manifest.ShadowDirectory, manifest.FrameworkAssemblyPath, StringComparison.OrdinalIgnoreCase);
        Assert.All(
            manifest.ManagedAssemblies,
            path => Assert.StartsWith(manifest.ShadowDirectory, path, StringComparison.OrdinalIgnoreCase));
        Assert.All(
            manifest.NativeAssets,
            path => Assert.StartsWith(manifest.ShadowDirectory, path, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class TempWorkspace : IDisposable
    {
        public TempWorkspace()
        {
            Root = Path.Combine(Path.GetTempPath(), "DevTools", "NUnit", "BuilderTests", Guid.NewGuid().ToString("N"));
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
