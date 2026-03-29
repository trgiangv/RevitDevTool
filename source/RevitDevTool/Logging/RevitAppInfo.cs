using DevTools.Logging.Abstractions;
using DevTools.Utilities;
using RevitDevTool.Core;

namespace RevitDevTool.Logging;

public sealed class RevitAppInfo : IAppInfo
{
    public string AppName => "Revit";
    public string VersionBuild => RevitContext.Application.VersionBuild ?? "unknown";
    public int ProcessId => AppUtils.CurrentProcessId;
}
