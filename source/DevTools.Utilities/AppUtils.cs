using System.IO;
using Autodesk.Windows;

namespace DevTools.Utilities;

public static class AppUtils
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
    /// Show a folder selection dialog and return the selected folder path.
    /// </summary>
    /// <param name="title">Dialog description</param>
    /// <param name="owner">Owner window handle for the dialog</param>
    /// <returns>Selected folder path or empty string if cancelled</returns>
    public static string SelectFolder(string title, IntPtr? owner = null)
    {
        owner ??= ComponentManager.ApplicationWindow;
        using var dialog = new Microsoft.WindowsAPICodePack.Dialogs.CommonOpenFileDialog();
        dialog.Title = title;
        dialog.IsFolderPicker = true;
        dialog.Multiselect = false;
        return dialog.ShowDialog(owner.Value) == Microsoft.WindowsAPICodePack.Dialogs.CommonFileDialogResult.Ok
            ? dialog.FileName
            : string.Empty;
    }
}
