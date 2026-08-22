using DevTools.Testing.Host.Loading;

namespace DevTools.Testing.Host.Tests.Loading;

public sealed class TestingGenerationFilesTests
{
    [Theory]
    [InlineData("sample.pdb", TestingGenerationFileKind.Symbols)]
    [InlineData("native.dll", TestingGenerationFileKind.Native)]
    [InlineData("readme.txt", TestingGenerationFileKind.Other)]
    public void Classify_handles_non_managed_outputs(string fileName, TestingGenerationFileKind expected)
    {
        using var workspace = new TemporaryDirectory();
        var path = Path.Combine(workspace.Path, fileName);
        File.WriteAllText(path, "not-a-pe");

        Assert.Equal(expected, TestingGenerationFiles.Classify(path));
    }

    [Fact]
    public void IsVolatileOutput_delegates_to_generation_paths()
    {
        Assert.True(TestingGenerationPaths.IsVolatileGenerationOutput(@"TestResults\out.trx"));
        Assert.True(TestingGenerationPaths.IsVolatileGenerationOutput(@"Log\host.log"));
        Assert.False(TestingGenerationPaths.IsVolatileGenerationOutput(@"bin\sample.dll"));
    }

    sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"generation-files-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
