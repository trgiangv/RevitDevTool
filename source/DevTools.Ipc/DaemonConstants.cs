namespace DevTools.Ipc;

/// <summary>
/// Shared constants for Daemon control pipe communication.
/// Used by both the Daemon itself and host add-in clients.
/// </summary>
public static class DaemonConstants
{
    /// <summary>
    /// Pytest/control pipe prefix (length-prefixed <c>BridgeMessage</c>).
    /// Format: <c>{PytestPipePrefix}_{Host}_{Version}_{PID}</c>
    /// </summary>
    public const string PytestPipePrefix = "DevTools";

    /// <summary>
    /// SDK MCP pipe prefix (newline-delimited JSON-RPC).
    /// Format: <c>{McpPipePrefix}_{Host}_{Version}_{PID}</c>
    /// </summary>
    public const string McpPipePrefix = "DevToolsMcp";

    public const string ControlPipeName = "DevToolsDaemon_Control";
    public const string TrayIconResourceKey = "TrayIcon";
    public const string StdioArg = "--stdio";
    public const string StartupErrorTitle = "DevTools Daemon \u2014 Startup Error";
    public const int ShutdownTimeoutSeconds = 5;

    /// <summary>
    /// Method names for the daemon's control pipe commands.
    /// </summary>
    public static class Methods
    {
        public const string Status = "daemon/status";
        public const string AuthState = "daemon/auth_state";
        public const string SignIn = "daemon/trigger_signin";
        public const string SignOut = "daemon/trigger_signout";
        public const string ConnectedHosts = "daemon/connected_hosts";
        public const string OpenDashboard = "daemon/open_dashboard";
    }

    /// <summary>
    /// Route paths for the daemon's HTTP API endpoints.
    /// </summary>
    public static class RoutePaths
    {
        public const string Tunnel = "/tunnel";
        public const string Machines = "/machines";
    }

    public static class JsonProperties
    {
        public const string IsRunning = "isRunning";
    }

    public static class Errors
    {
        public const string UnknownMethod = "unknown_method";
        public const string InvalidRequest = "invalid_request";
    }
}
