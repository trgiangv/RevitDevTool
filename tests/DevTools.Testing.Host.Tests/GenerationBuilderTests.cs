using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Host;
using DevTools.Testing.Host.Loading;

namespace DevTools.Testing.Host.Tests;

public sealed class GenerationBuilderTests
{
    [Fact]
    public void Build_snapshots_test_output_and_indexes_managed_assets()
    {
        var root = Path.Combine(Path.GetTempPath(), "DevTools.Testing.Host.Tests", Guid.NewGuid().ToString("N"));
        var output = Path.Combine(root, "out");
        var generations = Path.Combine(root, "gen");
        Directory.CreateDirectory(Path.Combine(output, "Log"));
        Directory.CreateDirectory(generations);

        var assembly = CopySelf(output, "Sample.Tests.dll");
        var runtime = CopySelf(output, "Runtime.dll");
            var framework = CopySelf(output, "provider-support.dll");
        File.WriteAllText(Path.Combine(output, "Log", "noise.log"), "skip");
        File.WriteAllText(Path.Combine(output, "readme.txt"), "content");

        try
        {
            var builder = new TestingGenerationBuilder(generations);
            var manifest = builder.Build(new FixedPolicy(new TestingGenerationPlan(
                "provider.example",
                assembly,
                [
                    new TestingGenerationFile(assembly, "Sample.Tests.dll", TestingGenerationFileKind.Managed),
                    new TestingGenerationFile(runtime, "Runtime.dll", TestingGenerationFileKind.Managed),
                    new TestingGenerationFile(framework, "provider-support.dll", TestingGenerationFileKind.Managed),
                    new TestingGenerationFile(Path.Combine(output, "readme.txt"), "readme.txt", TestingGenerationFileKind.Other),
                ],
                "Runtime.dll")), assembly);

            Assert.False(string.IsNullOrWhiteSpace(manifest.GenerationId));
            Assert.True(File.Exists(manifest.ShadowAssemblyPath));
            Assert.True(File.Exists(manifest.RuntimeAssemblyPath));
            Assert.Contains(manifest.ManagedAssemblies, path => path.EndsWith("Sample.Tests.dll", StringComparison.OrdinalIgnoreCase));
            Assert.False(Directory.Exists(Path.Combine(manifest.ShadowDirectory, "Log")));
            Assert.True(File.Exists(Path.Combine(manifest.ShadowDirectory, "readme.txt")));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    static string CopySelf(string outputDirectory, string fileName)
    {
        var destination = Path.Combine(outputDirectory, fileName);
        File.Copy(typeof(GenerationBuilderTests).Assembly.Location, destination, overwrite: true);
        return destination;
    }

    private sealed class FixedPolicy(TestingGenerationPlan plan) : ITestingGenerationPolicy
    {
        public TestingGenerationPlan CreatePlan(string testAssemblyPath) => plan;
        public void ValidatePublished(TestingGenerationManifest manifest) { }
    }
}
