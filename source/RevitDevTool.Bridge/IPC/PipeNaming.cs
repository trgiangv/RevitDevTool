namespace RevitDevTool.Bridge.IPC;

/// <summary>
/// Centralizes the pipe naming convention <c>RevitDevTool_{appId}_{version}_{pid}</c>
/// shared by EngineHost (server), ConnectionManager (client), and host launchers (discovery).
/// </summary>
public static class PipeNaming
{
    public const string Prefix = "RevitDevTool";
    private const char Separator = '_';

    public static string Build(string appId, string version, int processId) =>
        $"{Prefix}{Separator}{appId}{Separator}{version}{Separator}{processId}";

    public static bool TryParse(string pipeName, out string appId, out string version, out int pid)
    {
        appId = "";
        version = "";
        pid = 0;
        var parts = pipeName.Split(Separator);
        return parts.Length == 4
               && parts[0] == Prefix
               && (appId = parts[1]).Length > 0
               && (version = parts[2]).Length > 0
               && int.TryParse(parts[3], out pid);
    }

    public static string WildcardPattern => $"{Prefix}{Separator}*";

    public static string AppWildcard(string appId) =>
        $"{Prefix}{Separator}{appId}{Separator}*";

    public static string VersionWildcard(string appId, string version) =>
        $"{Prefix}{Separator}{appId}{Separator}{version}{Separator}*";
}
