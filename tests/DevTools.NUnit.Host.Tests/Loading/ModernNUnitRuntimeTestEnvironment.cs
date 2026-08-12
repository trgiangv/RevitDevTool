using DevTools.NUnit.Host.Loading;

namespace DevTools.NUnit.Host.Tests.Loading;

internal static class ModernNUnitRuntimeTestEnvironment
{
    public static string RuntimeAssemblyPath { get; } = Path.Combine(
        NUnitGenerationTestEnvironment.RepositoryRoot,
        "source",
        "DevTools.NUnit.Runtime",
        "bin",
        "Debug",
        "net10.0-windows",
        NUnitGenerationBuilder.RuntimeAssemblyFileName);

    public static string RuntimeSymbolPath { get; } = Path.Combine(
        NUnitGenerationTestEnvironment.RepositoryRoot,
        "source",
        "DevTools.NUnit.Runtime",
        "bin",
        "Debug",
        "net10.0-windows",
        NUnitGenerationBuilder.RuntimeSymbolFileName);

    public static NUnitGenerationBuilder CreateBuilder(string generationsRoot) =>
        new(
            () => new NUnitRuntimeSource(
                RuntimeAssemblyPath,
                File.Exists(RuntimeSymbolPath) ? RuntimeSymbolPath : null,
                RuntimeDependencyPaths()),
            generationsRoot);

    private static IReadOnlyList<string> RuntimeDependencyPaths() =>
        new[] { "System.Reflection.Metadata.dll", "System.Collections.Immutable.dll" }
            .Select(name => Path.Combine(Path.GetDirectoryName(RuntimeAssemblyPath)!, name))
            .Where(File.Exists)
            .ToList();

    public static NUnitGenerationManifest BuildFixtureGeneration()
    {
        using var workspace = new TempWorkspace();
        var testAssembly = NUnitGenerationTestEnvironment.CreateGenerationOneAssembly(
            workspace.Root,
            "modern-runtime");
        var generationsRoot = NUnitGenerationTestEnvironment.CreateIsolatedGenerationsRoot();
        var builder = CreateBuilder(generationsRoot);
        return builder.Build(testAssembly);
    }

    public static NUnitGenerationManifest BuildGenerationWithDuplicateNativeAssets(string parentDirectory)
    {
        var testAssembly = NUnitGenerationTestEnvironment.CreateFixtureWorkspace(
            parentDirectory,
            "duplicate-native",
            outputDirectory =>
            {
                WriteNativeAsset(outputDirectory, "win-x64", 0xA1);
                WriteNativeAsset(outputDirectory, "win-x86", 0xB2);
                WriteNativeDepsJson(outputDirectory, "win-x64");
            });

        var generationsRoot = NUnitGenerationTestEnvironment.CreateIsolatedGenerationsRoot();
        return CreateBuilder(generationsRoot).Build(testAssembly);
    }

    public static NUnitGenerationManifest BuildGenerationWithUniqueNativeAsset(string parentDirectory)
    {
        var testAssembly = NUnitGenerationTestEnvironment.CreateFixtureWorkspace(
            parentDirectory,
            "unique-native",
            outputDirectory => WriteNativeAsset(outputDirectory, "win-x64", 0xC3));

        var generationsRoot = NUnitGenerationTestEnvironment.CreateIsolatedGenerationsRoot();
        return CreateBuilder(generationsRoot).Build(testAssembly);
    }

    private static void WriteNativeAsset(string outputDirectory, string rid, byte markerByte)
    {
        var nativeDirectory = Path.Combine(outputDirectory, "runtimes", rid, "native");
        Directory.CreateDirectory(nativeDirectory);
        File.WriteAllBytes(
            Path.Combine(nativeDirectory, "sample.native.dll"),
            [0x4D, 0x5A, markerByte, 0x00]);
    }

    private static void WriteNativeDepsJson(string outputDirectory, string preferredRid)
    {
        var testAssemblyFileName = Path.GetFileName(
            Directory.GetFiles(outputDirectory, "DevTools.NUnit.Runtime.Fixtures.dll").Single());

        var depsPath = Path.Combine(outputDirectory, Path.ChangeExtension(testAssemblyFileName, ".deps.json"));
        if (File.Exists(depsPath))
            return;

        var nativeRelativePath = Path.Combine("runtimes", preferredRid, "native", "sample.native.dll")
            .Replace('\\', '/');

        File.WriteAllText(
            depsPath,
            $$"""
              {
                "runtimeTarget": {
                  "name": ".NETCoreApp,Version=v10.0",
                  "signature": ""
                },
                "targets": {
                  ".NETCoreApp,Version=v10.0": {
                    "DevTools.NUnit.Runtime.Fixtures/1.0.0": {
                      "runtime": {
                        "{{testAssemblyFileName}}": {}
                      },
                      "native": {
                        "{{nativeRelativePath}}": {
                          "fileVersion": "0.0.0.0"
                        }
                      }
                    }
                  }
                },
                "libraries": {
                  "DevTools.NUnit.Runtime.Fixtures/1.0.0": {
                    "type": "project",
                    "serviceable": false,
                    "sha512": ""
                  }
                }
              }
              """);
    }

    private sealed class TempWorkspace : IDisposable
    {
        public TempWorkspace()
        {
            Root = Path.Combine(
                Path.GetTempPath(),
                "DevTools",
                "NUnit",
                "ModernRuntimeTests",
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
