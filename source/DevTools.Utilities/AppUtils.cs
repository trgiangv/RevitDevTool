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

    public static int CurrentProcessId =>
#if NET8_0_OR_GREATER
        Environment.ProcessId;
#else
        System.Diagnostics.Process.GetCurrentProcess().Id;
#endif

    /// <summary>
    /// Show a folder selection dialog and return the selected folder path.
    /// </summary>
    /// <param name="title">Dialog description</param>
    /// <param name="owner">Owner window for the dialog</param>
    /// <returns>Selected folder path or empty string if cancelled</returns>
    public static string SelectFolder(string title, Window? owner = null)
    {
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
        return dialog.ShowDialog(owner) == Microsoft.WindowsAPICodePack.Dialogs.CommonFileDialogResult.Ok
            ? dialog.FileName
            : string.Empty;
#endif
    }
}
