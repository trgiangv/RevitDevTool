namespace DevTools.Logging;

public interface IAppInfo
{
    string AppName { get; }
    string VersionBuild { get; }
    int ProcessId { get; }
}
