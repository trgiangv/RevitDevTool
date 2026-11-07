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
