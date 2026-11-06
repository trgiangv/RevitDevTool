using DevTools.AssemblyIsolation;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Host.Loading;
using DevTools.Testing.Host.NUnit;
using DevTools.Testing.Host.Runtime;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;

namespace DevTools.NUnit.Host.Tests.Loading;

internal static class NUnitRuntimeTestEnvironment
{
    public const string HostName = "Revit";

    public static string FrameworkPath(TestingGenerationManifest manifest) =>
        Path.Combine(manifest.ShadowDirectory, NUnitGenerationTestEnvironment.FrameworkFileName);

    public static Assembly LoadFramework(TestingGenerationManifest manifest) =>
        NUnitFrameworkHostShare.GetOrLoadFromShadow(FrameworkPath(manifest), HostName);

    public static AssemblyIsolationPlan CreatePlan(TestingGenerationManifest manifest, Assembly framework) =>
        OutputSessionFactory.CreatePlan(manifest, framework, NullLogger.Instance);

    public static OutputSessionFactory CreateFactory() =>
        new(
            TestFrameworkId.NUnit,
            NullLogger.Instance,
            frameworkPath => NUnitFrameworkHostShare.GetOrLoadFromShadow(frameworkPath, HostName));

    public static NUnitGenerationHarness CreateBuilder(string generationsRoot) =>
        new(new TestingGenerationStore(generationsRoot), new NUnitGenerationPolicy());

    public static TestingGenerationManifest BuildFixtureGeneration()
    {
        using var workspace = new TempWorkspace();
        var testAssembly = NUnitGenerationTestEnvironment.CreateGenerationOneAssembly(
            workspace.Root,
            "modern-runtime");
        var generationsRoot = NUnitGenerationTestEnvironment.CreateIsolatedGenerationsRoot();
        var builder = CreateBuilder(generationsRoot);
        return builder.Build(testAssembly);
    }

    public static TestingGenerationManifest BuildGenerationWithDuplicateNativeAssets(string parentDirectory)
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

    public static TestingGenerationManifest BuildGenerationWithUniqueNativeAsset(string parentDirectory)
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
            Root = Path.Combine(Path.GetTempPath(), "DevTools.nunit." + Guid.NewGuid().ToString("N"));
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
