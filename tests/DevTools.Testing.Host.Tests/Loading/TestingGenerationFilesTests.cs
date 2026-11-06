using DevTools.Testing.Host.Loading;

namespace DevTools.Testing.Host.Tests.Loading;

[TestClass]
public sealed class TestingGenerationFilesTests
{
    [TestMethod]
    [DataRow("sample.pdb")]
    [DataRow("native.dll")]
    [DataRow("readme.txt")]
    public void IsManagedIdentity_rejects_non_assemblies(string fileName)
    {
        using var workspace = new TemporaryDirectory();
        var path = Path.Combine(workspace.Path, fileName);
        File.WriteAllText(path, "not-a-pe");

        Assert.IsFalse(TestingGenerationFiles.IsManagedIdentity(path));
    }

    [TestMethod]
    public void Public_path_helpers_match_internal_generation_paths()
    {
        Assert.IsTrue(TestingGenerationFiles.IsVolatileGenerationOutput(@"TestResults\out.trx"));
        Assert.IsTrue(TestingGenerationFiles.IsVolatileGenerationOutput(@"Log\host.log"));
        Assert.IsFalse(TestingGenerationFiles.IsVolatileGenerationOutput(@"bin\sample.dll"));
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
