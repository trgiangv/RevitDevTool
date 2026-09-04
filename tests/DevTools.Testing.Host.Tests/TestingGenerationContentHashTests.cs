using DevTools.Testing.Host.Loading;

namespace DevTools.Testing.Host.Tests;

public sealed class TestingGenerationContentHashTests
{
    [Fact]
    public void ComputeGenerationId_is_stable_for_the_same_content()
    {
        var directory = Directory.CreateTempSubdirectory("generation-hash-").FullName;
        try
        {
            var firstPath = Path.Combine(directory, "alpha.txt");
            var secondPath = Path.Combine(directory, "beta.txt");
            File.WriteAllText(firstPath, "alpha");
            File.WriteAllText(secondPath, "beta");
            var entries = new[]
            {
                ("beta.txt", secondPath),
                ("alpha.txt", firstPath),
            };

            var first = TestingGenerationContentHash.ComputeGenerationId(entries);
            var second = TestingGenerationContentHash.ComputeGenerationId(entries.Reverse());

            Assert.Equal(first, second);
            Assert.Matches("^[0-9a-f]{64}$", first);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ComputeGenerationId_changes_when_file_content_changes()
    {
        var directory = Directory.CreateTempSubdirectory("generation-hash-").FullName;
        try
        {
            var path = Path.Combine(directory, "payload.txt");
            File.WriteAllText(path, "v1");
            var before = TestingGenerationContentHash.ComputeGenerationId([("payload.txt", path)]);
            File.WriteAllText(path, "v2");
            var after = TestingGenerationContentHash.ComputeGenerationId([("payload.txt", path)]);

            Assert.NotEqual(before, after);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
