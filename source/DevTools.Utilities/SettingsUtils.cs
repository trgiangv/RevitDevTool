using System.IO;
namespace DevTools.Utilities;

public static class SettingsUtils
{
    public static string AutodeskVersion
    {
        get
        {
#if AUTODESK2022
                return "2022";
#elif AUTODESK2023
                return "2022";
#elif AUTODESK2024
                return "2024";
#elif AUTODESK2025
                return "2025";
#elif AUTODESK2026
                return "2026";
#elif AUTODESK2027
                return "2027";
#else
                return "Unknown";
#endif
        }
    }
    
    public static string GetContentRootPath()
    {
        var appData = GetApplicationDataPath();
        var rootPath = Path.Combine(appData, AutodeskVersion);
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
}