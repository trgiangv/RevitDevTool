namespace DevTools.Telemetry;

public static class TelemetryKeys
{
    public static class Feature
    {
        public const string Execution = "execution";
        public const string Mcp = "mcp";
        public const string AppDomain = "appdomain.unhandled";
    }

    public static class Tag
    {
        public const string Feat = "telemetry.feature";
        public const string Kind = "telemetry.kind";
        public const string Provider = "provider";
        public const string InstallationId = "installation_id";
        public const string HostApp = "host_app";
        public const string HostVersion = "host_version";
        public const string HostBuild = "host_build";
    }

    public static class Extra
    {
        public const string Execution = "execution";
        public const string Mcp = "mcp";
        public const string Geometry = "geometry";
        public const string Logging = "logging";
        public const string Total = "total";
        public const string Succeeded = "succeeded";
        public const string Failed = "failed";
        public const string Providers = "providers";
        public const string Types = "types";
        public const string Levels = "levels";
    }

    public static class Breadcrumb
    {
        public const string Execution = "telemetry.execution";
        public const string Mcp = "telemetry.mcp";
        public const string Geometry = "telemetry.geometry";
    }

    public static class Event
    {
        public const string SessionUsage = "devtool.session.usage";
    }

    public static class Fingerprint
    {
        public const string DevTool = "devtool";
        public const string SessionUsage = "session-usage";
    }
}
