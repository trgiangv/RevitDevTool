using DevTools.Logging;
using RevitDevTool.Core;

namespace RevitDevTool.HostAdapters;

public sealed class RevitHostAppInfo : IHostAppInfo
{
    public string AppName => "Revit";
    public string VersionNumber => RevitContext.Application.VersionNumber;
    public string? VersionBuild => RevitContext.Application.VersionBuild;
    public int ProcessId => Environment.ProcessId;
}
