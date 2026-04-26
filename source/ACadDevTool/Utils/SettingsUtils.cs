using System.IO;
namespace AcadDevTool.Utils;

public static class SettingsUtils
{
    public static string AcadVersion
    {
        get
        {
            #if AUTOCAD2022
                return "2022";
            #elif AUTOCAD2023
                return "2022";
            #elif AUTOCAD2024
                return "2024";
            #elif AUTOCAD2025
                return "2025";
            #elif AUTOCAD2026
                return "2026";
            #elif AUTOCAD2027
                return "2027";
            #endif
        }
    }

    public static string GetContentRootPath()
    {
        var appData = GetApplicationDataPath();
        var rootPath = Path.Combine(appData, AcadVersion);
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

    public static string SelectFolder(string title)
    {
        var acadHandle = Autodesk.AutoCAD.ApplicationServices.Core.Application.MainWindow.Handle;
        return DevTools.Utilities.AppUtils.SelectFolder(title, acadHandle);
    }
}