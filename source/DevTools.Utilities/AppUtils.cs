namespace DevTools.Utilities;

public static class AppUtils
{
    public static string GetContentRootPath(string versionNumber)
    {
        var appData = GetApplicationDataPath();
        var rootPath = Path.Combine(appData, versionNumber);
        Directory.CreateDirectory(rootPath);
        return rootPath;
    }

    public static string GetApplicationDataPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var rootPath = Path.Combine(appData, "RevitDevTool");
        Directory.CreateDirectory(rootPath);
        return rootPath;
    }

    public static string GetBundleContentsPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Autodesk", "ApplicationPlugins", "RevitDevTool.bundle", "Contents");

    /// <summary>
    ///     The folder of the Velopack install (<c>%LOCALAPPDATA%\RevitDevTool\current</c>). Velopack replaces it at each update.
    /// </summary>
    public static string GetInstalledAppPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RevitDevTool", "current");

    /// <summary>
    ///     The Daemon of the Velopack install. If there is no install, the dev deploy in the bundle <c>Contents</c>.
    ///     It is the real executable, not the Velopack stub, because MCP clients need its stdin and stdout.
    /// </summary>
    public static string GetDaemonExePath() => FindApp(DaemonExe);

    public static string GetTestRunnerExePath() => FindApp(TestRunnerExe);

    private const string DaemonExe = "DevTools.Daemon.exe";
    private const string TestRunnerExe = "DevTools.TestRunner.exe";

    private static string FindApp(string fileName)
    {
        var installed = Path.Combine(GetInstalledAppPath(), fileName);
        return File.Exists(installed) ? installed : Path.Combine(GetBundleContentsPath(), fileName);
    }

    public static bool IsValidPath(string? path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            var root = Path.GetPathRoot(path);
            return !string.IsNullOrEmpty(root) && Directory.Exists(root);
        }
        catch
        {
            return false;
        }
    }
}
