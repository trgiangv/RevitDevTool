namespace RevitDevTool.Contracts;

public static class BridgeActions
{
    public const string Ping = "bridge.ping";
    public const string Pong = "bridge.pong";
    public const string ListTools = "tools.list";
    public const string ListPrompts = "prompts.list";
    public const string ListResources = "resources.list";
    public const string GetPrompt = "prompts.get";
    public const string ReadResource = "resources.read";
    public const string ToolCall = "tools.call";
    public const string GetExecution = "bridge.get_execution";
    public const string CancelExecution = "bridge.cancel_execution";
    public const string Shutdown = "bridge.shutdown";
    public const string ToolsChanged = "tools.changed";
}

public static class BridgeMessageKinds
{
    public const string Request = "request";
    public const string Response = "response";
    public const string Event = "event";
}

