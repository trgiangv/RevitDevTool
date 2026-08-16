using DevTools.UI;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace DevTools.Presentation;

public static class AppUtils
{
    /// <summary>
    /// Show a folder selection dialog and return the selected folder path.
    /// </summary>
    /// <param name="title">Dialog description</param>
    /// <param name="owner">Owner window handle for the dialog</param>
    /// <returns>Selected folder path or empty string if cancelled</returns>
    public static string SelectFolder(string title, IntPtr? owner = null)
    {
        owner ??= HostUiHelper.MainWindowHandle;
        using var dialog = new CommonOpenFileDialog();
        dialog.Title = title;
        dialog.IsFolderPicker = true;
        dialog.Multiselect = false;
        return dialog.ShowDialog(owner.Value) == CommonFileDialogResult.Ok
            ? dialog.FileName
            : string.Empty;
    }
}
