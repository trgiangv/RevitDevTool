namespace DevTools.McpParser.Models;

public static class ExecutionErrorCodes
{
    public const string UnknownAction = "bridge.unknown_action";
    public const string Disconnected = "bridge.disconnected";
    public const string InvalidResponse = "bridge.invalid_response";
    public const string Error = "bridge.error";

    public const string ExecutionMissingId = "execution.missing_id";
    public const string ExecutionNotFound = "execution.not_found";
    public const string QueueExecutionFailed = "queue.execution_failed";

    // Re-exports from DevTools.Mcp (backward compat)
    public const string ToolInvalidRequest = McpExecutionErrorCodes.ToolInvalidRequest;
    public const string ToolMissingName = McpExecutionErrorCodes.ToolMissingName;
    public const string ToolNotFound = McpExecutionErrorCodes.ToolNotFound;
    public const string ToolFailed = McpExecutionErrorCodes.ToolFailed;
    public const string ToolCancelled = McpExecutionErrorCodes.ToolCancelled;
    public const string ToolUnknownSourceKind = McpExecutionErrorCodes.ToolUnknownSourceKind;
    public const string ToolNotImplemented = McpExecutionErrorCodes.ToolNotImplemented;
    public const string ToolSourceNotFound = McpExecutionErrorCodes.ToolSourceNotFound;
    public const string ToolPythonRuntimeUnavailable = McpExecutionErrorCodes.ToolPythonRuntimeUnavailable;
    public const string ToolInvokeFailed = McpExecutionErrorCodes.ToolInvokeFailed;

    public const string PromptInvalidRequest = McpExecutionErrorCodes.PromptInvalidRequest;
    public const string PromptMissingName = McpExecutionErrorCodes.PromptMissingName;
    public const string PromptNotFound = McpExecutionErrorCodes.PromptNotFound;
    public const string PromptInvokeFailed = McpExecutionErrorCodes.PromptInvokeFailed;

    public const string ResourceInvalidRequest = McpExecutionErrorCodes.ResourceInvalidRequest;
    public const string ResourceMissingUri = McpExecutionErrorCodes.ResourceMissingUri;
    public const string ResourceNotFound = McpExecutionErrorCodes.ResourceNotFound;
    public const string ResourceReadFailed = McpExecutionErrorCodes.ResourceReadFailed;
}
