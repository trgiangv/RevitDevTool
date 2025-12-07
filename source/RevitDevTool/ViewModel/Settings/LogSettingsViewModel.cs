using System.IO;
using RevitDevTool.Models.Config;
using RevitDevTool.Services;
using Serilog;

namespace RevitDevTool.ViewModel.Settings;

public partial class LogSettingsViewModel : ObservableObject
{
    public static readonly LogSettingsViewModel Instance = new();
    public static int[] StackTraceDepths { get; } = [1, 2, 3, 4, 5]; // allowed maximum 5 levels
    
    [ObservableProperty] private bool _isSaveLogEnabled;
    [ObservableProperty] private LogSaveFormat _saveFormat;
    [ObservableProperty] private bool _includeStackTrace;
    [ObservableProperty] private RollingInterval _timeInterval;
    [ObservableProperty] private int _stackTraceDepth;
    [ObservableProperty] private string _filePath = string.Empty;

    private LogSettingsViewModel()
    {
        LoadFromConfig();
    }

    partial void OnSaveFormatChanged(LogSaveFormat value)
    {
        if (string.IsNullOrWhiteSpace(FilePath))
            return;
        
        try
        {
            var directory = Path.GetDirectoryName(FilePath);
            var filenameWithoutExtension = Path.GetFileNameWithoutExtension(FilePath);
            FilePath = Path.Combine(directory ?? string.Empty, $"{filenameWithoutExtension}.{FileExtension}");
        }
        catch (Exception e)
        {
            FilePath = SettingsLocation.GetDefaultLogPath(FileExtension);
        }
    }

    private void LoadFromConfig()
    {
        var config = SettingsService.Instance.LogConfig;
        IsSaveLogEnabled = config.IsSaveLogEnabled;
        SaveFormat = config.SaveFormat;
        IncludeStackTrace = config.IncludeStackTrace;
        StackTraceDepth = config.StackTraceDepth;
        TimeInterval = config.TimeInterval;
        FilePath = CorrectFilePath(config.FilePath, FileExtension);
    }

    public void SaveToConfig()
    {
        var config = SettingsService.Instance.LogConfig;
        config.IsSaveLogEnabled = IsSaveLogEnabled;
        config.SaveFormat = SaveFormat;
        config.IncludeStackTrace = IncludeStackTrace;
        config.StackTraceDepth = StackTraceDepth;
        config.TimeInterval = TimeInterval;
        config.FilePath = CorrectFilePath(FilePath, FileExtension);
        SettingsService.Instance.SaveSettings();
    }
    
    private static string CorrectFilePath(string originalFilePath, string fileExtension)
    {
        if (string.IsNullOrWhiteSpace(originalFilePath)) return SettingsLocation.GetDefaultLogPath(fileExtension);
        if (File.Exists(originalFilePath)) return originalFilePath;
        
        try
        {
            var directory = Path.GetDirectoryName(originalFilePath);
            var filenameWithoutExtension = Path.GetFileNameWithoutExtension(originalFilePath);
            var extension = Path.GetExtension(originalFilePath);
            
            if (!File.Exists(originalFilePath) && Directory.Exists(Path.GetDirectoryName(originalFilePath)))
            {
                return Path.Combine(Path.GetDirectoryName(originalFilePath)!,
                    $"{filenameWithoutExtension}.{extension}");
            }
            
            Directory.CreateDirectory(directory!);
            return Path.Combine(directory!, $"{filenameWithoutExtension}.{extension}");
        }
        catch
        {
            return SettingsLocation.GetDefaultLogPath(fileExtension);
        }
    }
    
    private string FileExtension => SaveFormat switch
    {
        LogSaveFormat.Sqlite => "db",
        LogSaveFormat.Json => "json",
        LogSaveFormat.Clef => "clef",
        _ => "log"
    };

    [RelayCommand]
    private void BrowseFile()
    {
        var filter = SaveFormat switch
        {
            LogSaveFormat.Sqlite => "SQLite Database (*.db)|*.db",
            LogSaveFormat.Json => "JSON Files (*.json)|*.json",
            LogSaveFormat.Clef => "CLEF Files (*.clef)|*.clef",
            _ => "All Files (*.*)|*.*"
        };
        
        var date = TimeInterval switch
        {
            RollingInterval.Infinite => "",
            RollingInterval.Year => DateTime.Now.ToString("yyyy"),
            RollingInterval.Month => DateTime.Now.ToString("yyyyMM"),
            RollingInterval.Day => DateTime.Now.ToString("yyyyMMdd"),
            RollingInterval.Hour => DateTime.Now.ToString("yyyyMMdd_HH"),
            RollingInterval.Minute => DateTime.Now.ToString("yyyyMMdd_HHmm"),
            _ => ""
        };
        
        var extension= FileExtension;
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = filter,
            DefaultExt = extension,
            FileName = $"log{date}.{extension}"
        };

        if (dialog.ShowDialog() == true)
        {
            FilePath = dialog.FileName;
        }
    }
}

