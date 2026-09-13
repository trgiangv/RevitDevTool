using DevTools.Testing.Host.Loading;

namespace DevTools.Testing.Host.Tests.Loading;

[TestClass]
public sealed class TestingGenerationFilesTests
{
    [TestMethod]
    [DataRow("sample.pdb", TestingGenerationFileKind.Symbols)]
    [DataRow("native.dll", TestingGenerationFileKind.Native)]
    [DataRow("readme.txt", TestingGenerationFileKind.Other)]
    public void Classify_handles_non_managed_outputs(string fileName, TestingGenerationFileKind expected)
    {
        using var workspace = new TemporaryDirectory();
        var path = Path.Combine(workspace.Path, fileName);
        File.WriteAllText(path, "not-a-pe");

        Assert.AreEqual(expected, TestingGenerationFiles.Classify(path));
    }

    [TestMethod]
    public void Public_path_helpers_match_internal_generation_paths()
    {
        Assert.IsTrue(TestingGenerationFiles.IsVolatileGenerationOutput(@"TestResults\out.trx"));
        Assert.IsTrue(TestingGenerationFiles.IsVolatileGenerationOutput(@"Log\host.log"));
        Assert.IsFalse(TestingGenerationFiles.IsVolatileGenerationOutput(@"bin\sample.dll"));
        Assert.AreEqual(@"folder\file.dll", TestingGenerationFiles.NormalizeRelativePath("folder/file.dll"));
    }

    [TestMethod]
    public void GetRelativePath_returns_a_path_under_the_root()
    {
        using var workspace = new TemporaryDirectory();
        var nested = Path.Combine(workspace.Path, "nested");
        Directory.CreateDirectory(nested);
        var file = Path.Combine(nested, "sample.dll");
        File.WriteAllText(file, "x");

        var relative = TestingGenerationFiles.GetRelativePath(workspace.Path, file);
        Assert.IsFalse(Path.IsPathRooted(relative));
        Assert.Contains("sample.dll", relative, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void ContentEquals_returns_true_for_identical_files()
    {
        using var workspace = new TemporaryDirectory();
        var first = Path.Combine(workspace.Path, "first.bin");
        var second = Path.Combine(workspace.Path, "second.bin");
        File.WriteAllText(first, "same-content");
        File.WriteAllText(second, "same-content");

        Assert.IsTrue(TestingGenerationFiles.ContentEquals(first, second));
    }

    [TestMethod]
    public void ContentEquals_returns_false_for_different_files()
    {
        using var workspace = new TemporaryDirectory();
        var first = Path.Combine(workspace.Path, "first.bin");
        var second = Path.Combine(workspace.Path, "second.bin");
        File.WriteAllText(first, "left");
        File.WriteAllText(second, "right");

        Assert.IsFalse(TestingGenerationFiles.ContentEquals(first, second));
    }

    [TestMethod]
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
        Assert.AreEqual(original, files["asset.bin"].SourcePath);

        TestingGenerationFiles.MergeFile(files, replacement, "asset.bin");
        Assert.AreEqual(replacement, files["asset.bin"].SourcePath);
        Assert.AreEqual(TestingGenerationFileKind.Other, files["asset.bin"].Kind);
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
