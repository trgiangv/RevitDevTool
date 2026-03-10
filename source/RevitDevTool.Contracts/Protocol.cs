namespace RevitDevTool.Contracts;

public static class McpProtocol
{
    public const string Version = "1.0";
    public const string SchemaVersion = "2026-03-08";
    public const string SchemaChecksum = "20260308b2";
}

public static class McpActions
{
    public const string Ping = "bridge.ping";
    public const string Pong = "bridge.pong";
    public const string ListTools = "tools.list";
    public const string ToolCall = "tool.call";
    public const string GetExecution = "bridge.get_execution";
    public const string CancelExecution = "bridge.cancel_execution";
    public const string Shutdown = "bridge.shutdown";
    public const string ToolsChanged = "tools.changed";
}

public static class McpMessageKinds
{
    public const string Request = "request";
    public const string Response = "response";
    public const string Event = "event";
}

public static class McpResultKinds
{
    public const string Empty = "empty";
    public const string Text = "text";
    public const string Json = "json";
}

public static class McpExecutionStates
{
    public const string Queued = "queued";
    public const string Running = "running";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
}
