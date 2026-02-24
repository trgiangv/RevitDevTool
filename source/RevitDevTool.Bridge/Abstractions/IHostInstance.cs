namespace RevitDevTool.Bridge.Abstractions;

/// <summary>
/// Represents a running instance of any host application (Revit, AutoCAD, Navisworks, Rhino, etc.)
/// connected via a named pipe.
/// </summary>
public interface IHostInstance
{
    string AppId { get; }
    string HostVersion { get; }
    int ProcessId { get; }
    string PipeName { get; }
}
