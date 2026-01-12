using RevitDevTool.Models.Config;
using System.IO;

namespace RevitDevTool.Utils;

public static class SettingsUtils
{
    /// <summary>
    /// Get the content root path for storing application data.
    /// </summary>
    /// <returns></returns>
    public static string GetContentRootPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var revitVersion = Context.Application.VersionNumber;
        var rootPath = Path.Combine(appData, "RevitDevTool", revitVersion);
        Directory.CreateDirectory(rootPath);
        return rootPath;
    }

    /// <summary>
    /// Check if the path is valid and the disk exists.
    /// Returns false if the path is empty, invalid, or the drive doesn't exist.
    /// </summary>
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

    /// <summary>
    /// Check if the directory of the path is writable.
    /// Returns false if the path is invalid or not writable.
    /// </summary>
    public static bool IsWritablePath(string? path)
    {
        try
        {
            if (!IsValidPath(path)) return false;

            var directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory))
                return false;

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Test write access by creating a temp file
            var testFile = Path.Combine(directory, $".write_test_{Guid.NewGuid():N}");
            File.WriteAllText(testFile, string.Empty);
            File.Delete(testFile);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get the file extension for the given log save format.
    /// </summary>
    /// <param name="format"></param>
    /// <returns></returns>
    public static string ToFileExtension(this LogSaveFormat format) => format switch
    {
        LogSaveFormat.Sqlite => "db",
        LogSaveFormat.Json => "json",
        LogSaveFormat.Clef => "clef",
        _ => "log"
    };
}