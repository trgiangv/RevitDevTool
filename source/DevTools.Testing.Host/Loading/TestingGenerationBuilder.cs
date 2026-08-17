using System.Collections.Concurrent;

namespace DevTools.Testing.Host.Loading;

public sealed class TestingGenerationBuilder
{
    private static readonly ConcurrentDictionary<string, object> GenerationLocks = new(StringComparer.OrdinalIgnoreCase);

    private readonly string _generationsRootDirectory;

    public TestingGenerationBuilder(string? generationsRootDirectory = null)
    {
        _generationsRootDirectory = generationsRootDirectory
            ?? Path.Combine(Path.GetTempPath(), "DevTools", "Testing", "Generations");
    }

    public TestingGenerationManifest Build(ITestingGenerationPolicy policy, string testAssemblyPath) =>
        new TestingGenerationStore(_generationsRootDirectory).Build(policy, testAssemblyPath);
}
