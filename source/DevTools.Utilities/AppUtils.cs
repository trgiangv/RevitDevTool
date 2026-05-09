namespace DevTools.Utilities;

public static class AppUtils
{
    public static string GetContentRootPath(string versionNumber)
    {
        var appData = GetApplicationDataPath();
        var rootPath = Path.Combine(appData, versionNumber);
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
        owner ??= HostUiHelper.MainWindowHandle;
        using var dialog = new Microsoft.WindowsAPICodePack.Dialogs.CommonOpenFileDialog();
        dialog.Title = title;
        dialog.IsFolderPicker = true;
        dialog.Multiselect = false;
        return dialog.ShowDialog(owner.Value) == Microsoft.WindowsAPICodePack.Dialogs.CommonFileDialogResult.Ok
            ? dialog.FileName
            : string.Empty;
    }
}
