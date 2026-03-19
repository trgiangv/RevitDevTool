using System.IO;
using RevitDevTool.Core;

namespace RevitDevTool.Utils;

public static class SettingsUtils
{
    public static string GetContentRootPath()
    {
        var appData = GetApplicationDataPath();
        var revitVersion = RevitContext.Application.VersionNumber;
        var rootPath = Path.Combine(appData, revitVersion);
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

    public static string SelectFolder(string title) =>
        DevTools.Utilities.AppUtils.SelectFolder(title, UIFramework.MainWindow.getMainWnd());
}