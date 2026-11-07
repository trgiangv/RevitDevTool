namespace DevTools.Utilities.Tests;

[TestClass]
public sealed class AppUtilsTests
{
    [TestMethod]
    public void GetApplicationDataPath_ends_with_RevitDevTool()
    {
        var path = AppUtils.GetApplicationDataPath();
        Assert.EndsWith("RevitDevTool", path, StringComparison.OrdinalIgnoreCase);
        Assert.IsTrue(Directory.Exists(path));
    }

    [TestMethod]
    public void GetContentRootPath_combines_version_under_app_data()
    {
        var version = "test-" + Guid.NewGuid().ToString("N");
        var path = AppUtils.GetContentRootPath(version);
        try
        {
            Assert.EndsWith(version, path, StringComparison.Ordinal);
            Assert.StartsWith(AppUtils.GetApplicationDataPath(), path, StringComparison.OrdinalIgnoreCase);
            Assert.IsTrue(Directory.Exists(path));
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [TestMethod]
    public void GetBundleContentsPath_points_under_Autodesk_bundle()
    {
        var path = AppUtils.GetBundleContentsPath();
        Assert.Contains("Autodesk", path, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RevitDevTool.bundle", path, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("Contents", path, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void GetDaemonExePath_prefers_the_installed_app_then_bundle_contents()
    {
        var contents = AppUtils.GetBundleContentsPath();
        var daemon = Path.Combine(AppUtils.GetInstalledAppPath(), "DevTools.Daemon.exe");

        Assert.AreEqual(File.Exists(daemon) ? daemon : Path.Combine(contents, "DevTools.Daemon.exe"), AppUtils.GetDaemonExePath());
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("not-a-root")]
    public void IsValidPath_returns_false_for_blank_or_rootless_paths(string? path)
    {
        Assert.IsFalse(AppUtils.IsValidPath(path));
    }

    [TestMethod]
    public void IsValidPath_returns_true_for_existing_drive_root()
    {
        var root = Path.GetPathRoot(Environment.CurrentDirectory)!;
        Assert.IsTrue(AppUtils.IsValidPath(root));
    }
}
