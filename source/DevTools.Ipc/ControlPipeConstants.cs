namespace DevTools.Ipc;

/// <summary>
/// Shared constants for daemon control pipe communication.
/// Used by both the daemon and host add-in clients.
/// </summary>
public static class ControlPipeConstants
{
    /// <summary>
    /// Pipe name format: <c>{PipePrefix}_{Host}_{Version}_{PID}</c>
    /// </summary>
    public const string PipePrefix = "DevTools";
    public const string ControlPipeName = "DevToolsDaemon_Control";

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
