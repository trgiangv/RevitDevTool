using DevTools.Utilities;

namespace DevTools.Utilities.Tests;

public sealed class AppUtilsTests
{
    [Fact]
    public void GetApplicationDataPath_ends_with_RevitDevTool()
    {
        var path = AppUtils.GetApplicationDataPath();
        Assert.EndsWith("RevitDevTool", path, StringComparison.OrdinalIgnoreCase);
        Assert.True(Directory.Exists(path));
    }

    [Fact]
    public void GetContentRootPath_combines_version_under_app_data()
    {
        var version = "test-" + Guid.NewGuid().ToString("N");
        var path = AppUtils.GetContentRootPath(version);
        try
        {
            Assert.EndsWith(version, path, StringComparison.Ordinal);
            Assert.StartsWith(AppUtils.GetApplicationDataPath(), path, StringComparison.OrdinalIgnoreCase);
            Assert.True(Directory.Exists(path));
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void GetBundleContentsPath_points_under_Autodesk_bundle()
    {
        var path = AppUtils.GetBundleContentsPath();
        Assert.Contains("Autodesk", path, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RevitDevTool.bundle", path, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("Contents", path, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetDaemonExePath_and_GetTestRunnerExePath_are_under_bundle_contents()
    {
        var contents = AppUtils.GetBundleContentsPath();
        Assert.Equal(Path.Combine(contents, "DevTools.Daemon.exe"), AppUtils.GetDaemonExePath());
        Assert.Equal(Path.Combine(contents, "DevTools.TestRunner.exe"), AppUtils.GetTestRunnerExePath());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-root")]
    public void IsValidPath_returns_false_for_blank_or_rootless_paths(string? path)
    {
        Assert.False(AppUtils.IsValidPath(path));
    }

    [Fact]
    public void IsValidPath_returns_true_for_existing_drive_root()
    {
        var root = Path.GetPathRoot(Environment.CurrentDirectory)!;
        Assert.True(AppUtils.IsValidPath(root));
    }
}
