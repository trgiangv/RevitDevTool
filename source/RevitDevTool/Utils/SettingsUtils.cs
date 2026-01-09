using System.IO;
namespace RevitDevTool.Utils ;

public static class SettingsUtils
{
    public static string GetSettingsPath(string configName)
    {
        var appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData).AppendPath("RevitDevTool").AppendPath(Context.Application.VersionNumber);
        var settingsDir = appdata.AppendPath("Settings");
        Directory.CreateDirectory(settingsDir);
        return appdata.AppendPath("Settings").AppendPath(configName);
    }

    public static string GetDefaultLogPath(string extension)
    {
        var appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData).AppendPath("RevitDevTool").AppendPath(Context.Application.VersionNumber);
        var logsDir = appdata.AppendPath("Logs");
        Directory.CreateDirectory(logsDir);
        return Path.Combine(logsDir, $"log.{extension}");
    }
}