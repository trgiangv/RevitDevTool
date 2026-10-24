namespace DevTools.Telemetry;

public static class TelemetryKeys
{
    public static class Operation
    {
        public const string Execution = "devtool.execution";
        public const string Mcp = "devtool.mcp";
        public const string Test = "devtool.test";
        public const string Visualization = "devtool.visualization";
    }

    public static class Tag
    {
        public const string Operation = "operation";
        public const string Provider = "provider";
        public const string HostApp = "host_app";
        public const string HostVersion = "host_version";
        public const string HostBuild = "host_build";
    }
}
