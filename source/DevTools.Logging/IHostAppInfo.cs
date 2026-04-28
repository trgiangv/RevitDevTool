namespace DevTools.Logging;

/// <summary>
/// Provides host application metadata consumed by logging, execution, and IPC services.
/// Each host app (Revit, AutoCAD, etc.) registers one implementation at startup.
/// </summary>
public interface IHostAppInfo
{
    string AppName { get; }
    string VersionNumber { get; }
    string? VersionBuild { get; }
    int ProcessId { get; }
}
