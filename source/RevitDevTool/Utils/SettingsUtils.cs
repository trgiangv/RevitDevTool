using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
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
    
    /// <summary>
    ///     Check if the current user has write access to the specified path
    /// </summary>
    public static bool CheckWriteAccess(string path)
    {
        try
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            var accessControl = new DirectoryInfo(path).GetAccessControl();
            var accessRules = accessControl.GetAccessRules(true, true, typeof(NTAccount));
            return accessRules.Cast<FileSystemAccessRule>().Any(rule => principal.IsInRole(rule.IdentityReference.Value) && (rule.FileSystemRights & FileSystemRights.WriteData) == FileSystemRights.WriteData);
        }
        catch
        {
            return false;
        }
    }
    
    public static bool CheckValidPath(string path)
    {
        try
        {
            return Path.GetPathRoot(path) is null || string.IsNullOrWhiteSpace(path);
        }
        catch
        {
            return false;
        }
    }
}