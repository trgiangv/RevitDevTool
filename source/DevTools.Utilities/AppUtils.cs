using System.IO;
using System.Windows;

namespace DevTools.Utilities;

public static class AppUtils
{
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
    /// <param name="owner">Owner window for the dialog</param>
    /// <returns>Selected folder path or empty string if cancelled</returns>
    public static string SelectFolder(string title, Window? owner = null)
    {
        using var dialog = new Microsoft.WindowsAPICodePack.Dialogs.CommonOpenFileDialog();
        dialog.Title = title;
        dialog.IsFolderPicker = true;
        dialog.Multiselect = false;
        var result = owner != null ? dialog.ShowDialog(owner) : dialog.ShowDialog();
        return result == Microsoft.WindowsAPICodePack.Dialogs.CommonFileDialogResult.Ok
            ? dialog.FileName
            : string.Empty;
    }
    
    /// <summary>
    /// Show a folder selection dialog and return the selected folder path.
    /// </summary>
    /// <param name="title">Dialog description</param>
    /// <param name="owner">Owner window handle for the dialog</param>
    /// <returns>Selected folder path or empty string if cancelled</returns>
    public static string SelectFolder(string title, IntPtr? owner = null)
    {
        using var dialog = new Microsoft.WindowsAPICodePack.Dialogs.CommonOpenFileDialog();
        dialog.Title = title;
        dialog.IsFolderPicker = true;
        dialog.Multiselect = false;
        var result = owner.HasValue ? dialog.ShowDialog(owner.Value) : dialog.ShowDialog();
        return result == Microsoft.WindowsAPICodePack.Dialogs.CommonFileDialogResult.Ok
            ? dialog.FileName
            : string.Empty;
    }
}
