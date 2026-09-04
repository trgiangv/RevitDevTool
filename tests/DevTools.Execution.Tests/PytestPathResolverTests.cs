using DevTools.Execution.External.Testing;

namespace DevTools.Execution.Tests;

public sealed class PytestPathResolverTests
{
    [Fact]
    public void ResolveWorkspaceRoot_UsesExplicitWorkspaceWhenProvided()
    {
        var workspace = CreateTempDirectory();
        try
        {
            var resolved = PytestPathResolver.ResolveWorkspaceRoot(workspace, Path.Combine(workspace, "tests"));
            Assert.Equal(Path.GetFullPath(workspace), resolved);
        }
        finally
        {
            TryDeleteDirectory(workspace);
        }
    }

    [Fact]
    public void ResolveWorkspaceRoot_DerivesFromTestRootDirectoryWhenWorkspaceEmpty()
    {
        var workspace = CreateTempDirectory();
        var testRoot = Path.Combine(workspace, "tests");
        Directory.CreateDirectory(testRoot);
        try
        {
            var resolved = PytestPathResolver.ResolveWorkspaceRoot(string.Empty, testRoot);
            Assert.Equal(Path.GetFullPath(testRoot), resolved);
        }
        finally
        {
            TryDeleteDirectory(workspace);
        }
    }

    [Fact]
    public void ResolveWorkspaceRoot_DerivesFromTestRootFileParentWhenNotDirectory()
    {
        var workspace = CreateTempDirectory();
        var testsDir = Path.Combine(workspace, "tests");
        Directory.CreateDirectory(testsDir);
        var testFile = Path.Combine(testsDir, "test_a.py");
        File.WriteAllText(testFile, "# stub");
        try
        {
            var resolved = PytestPathResolver.ResolveWorkspaceRoot(string.Empty, testFile);
            Assert.Equal(Path.GetFullPath(testsDir), resolved);
        }
        finally
        {
            TryDeleteDirectory(workspace);
        }
    }

    [Fact]
    public void ResolvePath_RootedPath_ReturnsFullPathUnchanged()
    {
        var rooted = Path.Combine(Path.GetTempPath(), "pytest-rooted-" + Guid.NewGuid().ToString("N"));
        var resolved = PytestPathResolver.ResolvePath(rooted, "C:\\ignored");
        Assert.Equal(Path.GetFullPath(rooted), resolved);
    }

    [Fact]
    public void ResolvePath_RelativePath_CombinesWithWorkspaceRoot()
    {
        var workspace = CreateTempDirectory();
        try
        {
            var resolved = PytestPathResolver.ResolvePath("tests/a.py", workspace);
            Assert.Equal(Path.GetFullPath(Path.Combine(workspace, "tests", "a.py")), resolved);
        }
        finally
        {
            TryDeleteDirectory(workspace);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "pytest-path-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // best effort
        }
    }
}
