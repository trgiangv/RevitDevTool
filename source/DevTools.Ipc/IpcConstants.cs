namespace DevTools.Ipc;

public static class IpcConstants
{
    public const string TestPipePrefix = "DevTools";
    public const string McpPipePrefix = "DevToolsMcp";
    public const string ControlPipeName = "DevToolsDaemon_Control";

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
