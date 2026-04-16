namespace DevTools.Logging.Abstractions;

public interface IAppInfo
{
    string AppName { get; }
    string VersionBuild { get; }
    int ProcessId { get; }
}
