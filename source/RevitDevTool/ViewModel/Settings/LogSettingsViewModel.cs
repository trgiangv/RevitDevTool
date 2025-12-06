using Autodesk.Windows;
using RevitDevTool.Models.Config;
using RevitDevTool.Services;
using UIFramework;

namespace RevitDevTool.ViewModel.Settings;

public partial class LogSettingsViewModel : ObservableObject
{
    public static readonly LogSettingsViewModel Instance = new();

    [ObservableProperty] private LogSaveFormat _saveFormat;
    [ObservableProperty] private bool _includeStackTrace;
    [ObservableProperty] private bool _useMultipleFiles;
    [ObservableProperty] private LogTimeInterval _timeInterval;
    [ObservableProperty] private string _filePath = string.Empty;
    [ObservableProperty] private string _folderPath = string.Empty;

    private LogSettingsViewModel()
    {
        LoadFromConfig();
    }

    private void LoadFromConfig()
    {
        var config = SettingsService.Instance.GeneralConfig.LogConfig;
        SaveFormat = config.SaveFormat;
        IncludeStackTrace = config.IncludeStackTrace;
        UseMultipleFiles = config.UseMultipleFiles;
        TimeInterval = config.TimeInterval;
        FilePath = config.FilePath;
        FolderPath = config.FolderPath;
    }

    public void SaveToConfig()
    {
        var config = SettingsService.Instance.GeneralConfig.LogConfig;
        config.SaveFormat = SaveFormat;
        config.IncludeStackTrace = IncludeStackTrace;
        config.UseMultipleFiles = UseMultipleFiles;
        config.TimeInterval = TimeInterval;
        config.FilePath = FilePath;
        config.FolderPath = FolderPath;
        SettingsService.Instance.SaveSettings();
    }

    [RelayCommand]
    private void BrowseFile()
    {
        var extension = SaveFormat switch
        {
            LogSaveFormat.Sqlite => "db",
            LogSaveFormat.Json => "json",
            LogSaveFormat.Xml => "xml",
            LogSaveFormat.Csv => "csv",
            _ => "log"
        };

        var filter = SaveFormat switch
        {
            LogSaveFormat.Sqlite => "SQLite Database (*.db)|*.db",
            LogSaveFormat.Json => "JSON Files (*.json)|*.json",
            LogSaveFormat.Xml => "XML Files (*.xml)|*.xml",
            LogSaveFormat.Csv => "CSV Files (*.csv)|*.csv",
            _ => "All Files (*.*)|*.*"
        };

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = filter,
            DefaultExt = extension,
            FileName = $"log.{extension}"
        };

        if (dialog.ShowDialog() == true)
        {
            FilePath = dialog.FileName;
        }
    }

    [RelayCommand]
    private void BrowseFolder()
    {
        var owner = MainWindow.getMainWnd();
        
#if NET8_0_OR_GREATER
        Microsoft.Win32.OpenFolderDialog openFolderDialog = new()
        {
            Multiselect = false,
            Title = "Select Log Folder"
        };
        var result = openFolderDialog.ShowDialog(owner) == true ? openFolderDialog.FolderName : null;
#else
        var fbDlg = new Microsoft.WindowsAPICodePack.Dialogs.CommonOpenFileDialog();
        fbDlg.IsFolderPicker = true;
        fbDlg.Title = "Select Log Folder";
        var result = fbDlg.ShowDialog(owner) == Microsoft.WindowsAPICodePack.Dialogs.CommonFileDialogResult.Ok ? fbDlg.FileName : null;
#endif
        FolderPath = result ?? FolderPath;
    }
}

