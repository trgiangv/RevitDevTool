namespace DevTools.Ipc;

public static class IpcConstants
{
    public const string TestPipePrefix = "DevTools";
    public const string McpPipePrefix = "DevToolsMcp";
    public const string ControlPipeName = "DevToolsDaemon_Control";

    /// <summary>
    ///     Daemon start arguments. Without an argument, the Daemon opens its window.
    /// </summary>
    public static class DaemonArgs
    {
        public const string Background = "--background";
        public const string ShowUpdates = "--show-updates";
    }

    public static class Methods
    {
        public const string SignIn = "control/sign_in";
    }

    public static class RoutePaths
    {
        public const string Tunnel = "/tunnel";
        public const string Machines = "/machines";
    }
}
