using DevTools.Testing.Host.Loading;

namespace DevTools.Testing.Host.Tests;

public sealed class TestingGenerationPathsTests
{
    [Theory]
    [InlineData("Log/trace.log")]
    [InlineData("TestResults/output.trx")]
    [InlineData("bin/Debug/net10.0/app.diag")]
    [InlineData("artifacts/build.log")]
    public void IsVolatileGenerationOutput_detects_logs_and_diagnostics(string relativePath)
    {
        Assert.True(TestingGenerationPaths.IsVolatileGenerationOutput(relativePath));
    }

    [Theory]
    [InlineData("lib/Provider.dll")]
    [InlineData("content/config.json")]
    public void IsVolatileGenerationOutput_ignores_stable_outputs(string relativePath)
    {
        Assert.False(TestingGenerationPaths.IsVolatileGenerationOutput(relativePath));
    }

    [Fact]
    public void NormalizeRelativePath_converts_forward_slashes()
    {
        Assert.Equal("a\\b\\c.dll", TestingGenerationPaths.NormalizeRelativePath("a/b/c.dll"));
    }

    [Fact]
    public void GetRelativePath_returns_path_relative_to_root()
    {
        var root = Path.Combine(Path.GetTempPath(), "generation-root-" + Guid.NewGuid().ToString("N"));
        var nested = Path.Combine(root, "nested", "file.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(nested)!);
        File.WriteAllBytes(nested, [1]);
        try
        {
            var relative = TestingGenerationPaths.GetRelativePath(root, nested);
            Assert.Equal(Path.Combine("nested", "file.dll"), relative);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
