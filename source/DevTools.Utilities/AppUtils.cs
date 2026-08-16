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

    public static string GetDaemonExePath() => Path.Combine(GetBundleContentsPath(), "DevTools.Daemon.exe");

    public static string GetNUnitRunnerExePath() => Path.Combine(GetBundleContentsPath(), "DevTools.NUnit.Runner.exe");
    
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
