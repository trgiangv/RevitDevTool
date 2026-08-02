namespace DevTools.Mcp.Core;

public static class McpExecutionErrorCodes
{
    public const string ToolInvalidRequest = "tool.invalid_request";
    public const string ToolMissingName = "tool.missing_name";
    public const string ToolNotFound = "tool.not_found";
    public const string ToolFailed = "tool.failed";
    public const string ToolCancelled = "tool.cancelled";
    public const string ToolUnknownSourceKind = "tool.unknown_source_kind";
    public const string ToolNotImplemented = "tool.not_implemented";
    public const string ToolSourceNotFound = "tool.source_not_found";
    public const string ToolPythonRuntimeUnavailable = "tool.python_runtime_unavailable";
    public const string ToolInvokeFailed = "tool.invoke_failed";

    public const string PromptInvalidRequest = "prompt.invalid_request";
    public const string PromptMissingName = "prompt.missing_name";
    public const string PromptNotFound = "prompt.not_found";
    public const string PromptInvokeFailed = "prompt.invoke_failed";

    public const string ResourceInvalidRequest = "resource.invalid_request";
    public const string ResourceMissingUri = "resource.missing_uri";
    public const string ResourceNotFound = "resource.not_found";
    public const string ResourceReadFailed = "resource.read_failed";
}
