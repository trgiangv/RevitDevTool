using CommunityToolkit.Mvvm.ComponentModel;
using RevitDevTool.Bridge;
using RevitDevTool.Bridge.Revit;

namespace RevitDevTool.Desktop.Models;

public partial class QueueTaskItem : ObservableObject
{
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private int _index;
    [ObservableProperty] private string _filePath = string.Empty;
    [ObservableProperty] private string _fileName = string.Empty;
    [ObservableProperty] private string _fileDirectory = string.Empty;
    [ObservableProperty] private string _fullPath = string.Empty; // Full path display
    [ObservableProperty] private string _fileSize = string.Empty;
    [ObservableProperty] private long _fileSizeBytes;
    [ObservableProperty] private string _hostVersion = string.Empty;
    [ObservableProperty] private string _status = "Queued";
    [ObservableProperty] private string _instance = "-";
    [ObservableProperty] private long _durationMs;
    [ObservableProperty] private string _scriptOverride = string.Empty;
    [ObservableProperty] private string _scriptPath = string.Empty; // Path to script file (.py, .fsx, .dll)
    [ObservableProperty] private string _scriptType = string.Empty; // PY, DOTNET
    [ObservableProperty] private string _statusDetail = string.Empty;
    [ObservableProperty] private string _sourceIcon = "HardDrive"; // HardDrive, Cloud, Dns
    [ObservableProperty] private string _fileTypeColor = "#3B82F6"; // Blue for local, Orange for RevitServer, Purple for ACC
    [ObservableProperty] private string _scriptTypeColor = "#10B981"; // Green for PY, Red for .NET
    [ObservableProperty] private bool _hasWarning;
    [ObservableProperty] private string _warningMessage = string.Empty;

    // New fields from batch config
    [ObservableProperty] private bool _audit;
    [ObservableProperty] private string _detachFromCentral = string.Empty;
    [ObservableProperty] private string _workset = string.Empty;
    [ObservableProperty] private bool _openWorksets;
    [ObservableProperty] private bool _closeWorksets;
    [ObservableProperty] private bool _closeDocument = true;
    [ObservableProperty] private bool _closeHost;
    [ObservableProperty] private bool _isHeadless = true;

    public static QueueTaskItem FromResolvedJob(ResolvedJob job, int index)
    {
        var item = new QueueTaskItem
        {
            Index = index,
            FilePath = job.FilePath,
            FullPath = job.FilePath, // Full path display
            HostVersion = job.HostVersion,
            Status = "Queued",
            ScriptOverride = job.Script ?? string.Empty,
            ScriptPath = job.Script ?? string.Empty,
            CloseDocument = job.Lifecycle.CloseDocument,
            CloseHost = job.Lifecycle.CloseHost
        };

        // Get Open options - cast to RevitOpenOptions to access Revit-specific properties
        var revitOpen = (RevitOpenOptions)job.Open;
        item.IsHeadless = revitOpen.Headless;
        item.Audit = revitOpen.Audit;

        // DetachFromCentral and Workset are CentralMode and WorksetMode enums
        item.DetachFromCentral = revitOpen.DetachFromCentral.ToString();
        item.Workset = revitOpen.Workset.ToString();

        // OpenWorksets and CloseWorksets are List<int>
        item.OpenWorksets = revitOpen.OpenWorksets?.Count > 0;
        item.CloseWorksets = revitOpen.CloseWorksets?.Count > 0;

        // Determine script type and color
        if (!string.IsNullOrEmpty(job.Script))
        {
            var ext = Path.GetExtension(job.Script).ToLowerInvariant();
            if (ext == ".py")
            {
                item.ScriptType = "PY";
                item.ScriptTypeColor = "#10B981"; // Green
            }
            else if (ext == ".fsx" || ext == ".fs")
            {
                item.ScriptType = "FS";
                item.ScriptTypeColor = "#8B5CF6"; // Purple
            }
            else if (ext == ".dll")
            {
                item.ScriptType = "DOTNET";
                item.ScriptTypeColor = "#EF4444"; // Red
            }
            else
            {
                item.ScriptType = "SCRIPT";
                item.ScriptTypeColor = "#6B7280"; // Gray
            }
        }

        // Parse file info
        if (!string.IsNullOrEmpty(job.FilePath))
        {
            try
            {
                var fileInfo = new System.IO.FileInfo(job.FilePath);
                item.FileName = fileInfo.Name;
                item.FileDirectory = fileInfo.DirectoryName ?? string.Empty;
                item.FileSizeBytes = fileInfo.Exists ? fileInfo.Length : 0;
                item.FileSize = fileInfo.Exists ? FormatFileSize(fileInfo.Length) : "N/A";

                // Determine source icon and color
                if (job.FilePath.StartsWith("http", StringComparison.OrdinalIgnoreCase) ||
                    job.FilePath.Contains("://"))
                {
                    item.SourceIcon = "Cloud";
                    item.FileTypeColor = "#8B5CF6"; // Purple for ACC/Cloud
                }
                else if (job.FilePath.StartsWith("\\\\", StringComparison.OrdinalIgnoreCase))
                {
                    item.SourceIcon = "Dns"; // Revit Server
                    item.FileTypeColor = "#F97316"; // Orange for RevitServer
                }
                else
                {
                    item.SourceIcon = "HardDrive";
                    item.FileTypeColor = "#3B82F6"; // Blue for local
                }
            }
            catch
            {
                item.FileName = System.IO.Path.GetFileName(job.FilePath);
                item.FileDirectory = System.IO.Path.GetDirectoryName(job.FilePath) ?? string.Empty;
                item.FileSize = "N/A";
            }
        }

        return item;
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }
}
