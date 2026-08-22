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
    public void Public_path_helpers_match_internal_generation_paths()
    {
        Assert.True(TestingGenerationFiles.IsVolatileGenerationOutput(@"TestResults\out.trx"));
        Assert.True(TestingGenerationFiles.IsVolatileGenerationOutput(@"Log\host.log"));
        Assert.False(TestingGenerationFiles.IsVolatileGenerationOutput(@"bin\sample.dll"));
        Assert.Equal(@"folder\file.dll", TestingGenerationFiles.NormalizeRelativePath("folder/file.dll"));
    }

    [Fact]
    public void GetRelativePath_returns_a_path_under_the_root()
    {
        using var workspace = new TemporaryDirectory();
        var nested = Path.Combine(workspace.Path, "nested");
        Directory.CreateDirectory(nested);
        var file = Path.Combine(nested, "sample.dll");
        File.WriteAllText(file, "x");

        var relative = TestingGenerationFiles.GetRelativePath(workspace.Path, file);
        Assert.False(Path.IsPathRooted(relative));
        Assert.Contains("sample.dll", relative, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ContentEquals_returns_true_for_identical_files()
    {
        using var workspace = new TemporaryDirectory();
        var first = Path.Combine(workspace.Path, "first.bin");
        var second = Path.Combine(workspace.Path, "second.bin");
        File.WriteAllText(first, "same-content");
        File.WriteAllText(second, "same-content");

        Assert.True(TestingGenerationFiles.ContentEquals(first, second));
    }

    [Fact]
    public void ContentEquals_returns_false_for_different_files()
    {
        using var workspace = new TemporaryDirectory();
        var first = Path.Combine(workspace.Path, "first.bin");
        var second = Path.Combine(workspace.Path, "second.bin");
        File.WriteAllText(first, "left");
        File.WriteAllText(second, "right");

        Assert.False(TestingGenerationFiles.ContentEquals(first, second));
    }

    [Fact]
    public void MergeFile_replaces_existing_entry_only_when_content_differs()
    {
        using var workspace = new TemporaryDirectory();
        var original = Path.Combine(workspace.Path, "original.bin");
        var replacement = Path.Combine(workspace.Path, "replacement.bin");
        var unchanged = Path.Combine(workspace.Path, "unchanged.bin");
        File.WriteAllText(original, "version-one");
        File.WriteAllText(replacement, "version-two");
        File.WriteAllText(unchanged, "version-one");

        var files = new Dictionary<string, TestingGenerationFile>(StringComparer.OrdinalIgnoreCase)
        {
            ["asset.bin"] = new TestingGenerationFile(original, "asset.bin", TestingGenerationFileKind.Other),
        };

        TestingGenerationFiles.MergeFile(files, unchanged, "asset.bin");
        Assert.Equal(original, files["asset.bin"].SourcePath);

        TestingGenerationFiles.MergeFile(files, replacement, "asset.bin");
        Assert.Equal(replacement, files["asset.bin"].SourcePath);
        Assert.Equal(TestingGenerationFileKind.Other, files["asset.bin"].Kind);
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
