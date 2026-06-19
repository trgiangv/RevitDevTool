namespace DevTools.McpParser.Models;

public static class McpPropertyNames
{
    // Bridge envelope
    public const string ErrorMessage = "errorMessage";
    public const string Id = "id";
    public const string IsError = "isError";
    public const string Method = "method";
    public const string Params = "params";
    public const string Result = "result";

    // MCP payloads and tool arguments
    public const string Arguments = "arguments";
    public const string Code = "code";
    public const string FilePath = "filePath";
    public const string HostApp = "hostApp";
    public const string HostInstanceId = "hostInstanceId";
    public const string LanguageCode = "languageCode";
    public const string Name = "name";
    public const string Uri = "uri";
    public const string VersionNumber = "versionNumber";

    // MCP content and JSON Schema
    public const string Content = "content";
    public const string Description = "description";
    public const string Properties = "properties";
    public const string Required = "required";
    public const string Text = "text";
    public const string Type = "type";
}

public static class JsonSchemaTypeNames
{
    public const string Boolean = "boolean";
    public const string Integer = "integer";
    public const string Number = "number";
    public const string Object = "object";
    public const string String = "string";
}

/// <summary>
/// Shared constants for Daemon control pipe communication.
/// Used by both the Daemon itself and host add-in clients.
/// </summary>
public static class DaemonConstants
{
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
        public const string Method = "method";
        public const string IsRunning = "isRunning";
    }

    public static class Errors
    {
        public const string UnknownMethod = "unknown_method";
        public const string InvalidRequest = "invalid_request";
    }
}
