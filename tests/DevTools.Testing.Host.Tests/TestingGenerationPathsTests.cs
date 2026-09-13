using DevTools.Testing.Host.Loading;

namespace DevTools.Testing.Host.Tests;

[TestClass]
public sealed class TestingGenerationPathsTests
{
    [TestMethod]
    [DataRow("Log/trace.log")]
    [DataRow("TestResults/output.trx")]
    [DataRow("bin/Debug/net10.0/app.diag")]
    [DataRow("artifacts/build.log")]
    public void IsVolatileGenerationOutput_detects_logs_and_diagnostics(string relativePath)
    {
        Assert.IsTrue(TestingGenerationPaths.IsVolatileGenerationOutput(relativePath));
    }

    [TestMethod]
    [DataRow("lib/Provider.dll")]
    [DataRow("content/config.json")]
    public void IsVolatileGenerationOutput_ignores_stable_outputs(string relativePath)
    {
        Assert.IsFalse(TestingGenerationPaths.IsVolatileGenerationOutput(relativePath));
    }

    [TestMethod]
    public void NormalizeRelativePath_converts_forward_slashes()
    {
        Assert.AreEqual("a\\b\\c.dll", TestingGenerationPaths.NormalizeRelativePath("a/b/c.dll"));
    }

    [TestMethod]
    public void GetRelativePath_returns_path_relative_to_root()
    {
        var root = Path.Combine(Path.GetTempPath(), "generation-root-" + Guid.NewGuid().ToString("N"));
        var nested = Path.Combine(root, "nested", "file.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(nested)!);
        File.WriteAllBytes(nested, [1]);
        try
        {
            var relative = TestingGenerationPaths.GetRelativePath(root, nested);
            Assert.AreEqual(Path.Combine("nested", "file.dll"), relative);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
