namespace DevTools.Logging;

/// <summary>
/// Supported host products
/// </summary>
public enum HostApp
{
    Revit,
    AutoCad,
    Civil3D,
    Plant3D,
    AcadArch,
    AcadMech,
    AcadElec,
    AcadMep,
    AcadMap3D,
    Navisworks,
    Rhino,
    Tekla,
}


/// <summary>
/// Provides host application metadata consumed by logging, execution, and IPC services.
/// Each host app (Revit, AutoCAD, etc.) registers one implementation at startup.
/// </summary>
public interface IHostAppInfo
{
    HostApp Host { get; }
    string VersionNumber { get; }
    string? VersionBuild { get; }
    int ProcessId { get; }
}
