using DevTools.Logging;
using DevTools.Utilities;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace AcadDevTool.HostAdapters;

public sealed class AcadHostAppInfo : IHostAppInfo
{
    public string AppName => "AutoCAD";
    public string VersionNumber => SettingsUtils.AutodeskVersion;
    public string? VersionBuild => AcadApp.Version.ToString();
    public int ProcessId => Environment.ProcessId;
}
