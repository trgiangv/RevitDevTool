namespace DevTools.Ipc;

public static class IpcConstants
{
    public const string TestPipePrefix = "DevTools";
    public const string McpPipePrefix = "DevToolsMcp";
    public const string ControlPipeName = "DevToolsDaemon_Control";

    public static class Methods
    {
        public const string Status = "control/status";
        public const string AuthState = "control/auth_state";
        public const string SignIn = "control/sign_in";
        public const string SignOut = "control/sign_out";
        public const string ConnectedHosts = "control/connected_hosts";
        public const string OpenDashboard = "control/open_dashboard";
    }

    public static class RoutePaths
    {
        public const string Tunnel = "/tunnel";
        public const string Machines = "/machines";
    }
    
    public static class Errors
    {
        public const string UnknownMethod = "unknown_method";
        public const string InvalidRequest = "invalid_request";
    }
}
