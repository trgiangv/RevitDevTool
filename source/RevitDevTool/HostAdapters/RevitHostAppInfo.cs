using DevTools.Hosting;
using RevitDevTool.Core;

namespace RevitDevTool.HostAdapters;

public sealed class RevitHostAppInfo : IHostAppInfo
{
    public HostApp Host => HostApp.Revit;
    public string VersionNumber => RevitContext.Application.VersionNumber;
    public string? VersionBuild => RevitContext.Application.VersionBuild;
    public int ProcessId => Environment.ProcessId;
}
