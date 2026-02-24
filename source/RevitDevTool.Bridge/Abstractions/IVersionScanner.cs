namespace RevitDevTool.Bridge.Abstractions;

/// <summary>
/// Scans the local machine for installed versions of a host application.
/// </summary>
public interface IVersionScanner
{
    string AppId { get; }
    List<string> GetInstalledVersions();
}
