using RevitDevTool.Logging.Enums;
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
    /// Get the file extension for the given log save format.
    /// </summary>
    /// <param name="format"></param>
    /// <returns></returns>
    public static string ToFileExtension(this LogSaveFormat format) => format switch
    {
        LogSaveFormat.Json => "json",
        _ => "log"
    };

    /// <summary>
    /// Get the current process ID in a cross-platform manner.
    /// </summary>
    public static int CurrentProcessId =>
#if NET8_0_OR_GREATER
        Environment.ProcessId;
#else
        System.Diagnostics.Process.GetCurrentProcess().Id;
#endif

    /// <summary>
    /// Show a folder selection dialog and return the selected folder path. Use modern dialog on .NET 8+
    /// </summary>
    /// <param name="title">Dialog description</param>
    /// <returns>Selected folder path or empty string if cancelled</returns>
    public static string SelectFolder(string title)
    {
        var owner = UIFramework.MainWindow.getMainWnd();
#if NET8_0_OR_GREATER
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = title,
            Multiselect = false
        };
        return dialog.ShowDialog(owner) == true ? dialog.FolderName : string.Empty;
#else
        using var dialog = new Microsoft.WindowsAPICodePack.Dialogs.CommonOpenFileDialog();
        dialog.Title = title;
        dialog.IsFolderPicker = true;
        dialog.Multiselect = false;
        return dialog.ShowDialog(owner) == Microsoft.WindowsAPICodePack.Dialogs.CommonFileDialogResult.Ok ? dialog.FileName : string.Empty;
#endif
    }
}