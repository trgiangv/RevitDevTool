using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace RevitDevTool.Contracts;

public sealed record McpBridgePongBody
{
    public string? Endpoint { get; init; }
    public int Port { get; init; }
}

public sealed record McpToolsListResponseBody
{
    public IReadOnlyList<Tool> Tools { get; init; } = [];
}

public sealed record McpPromptsListResponseBody
{
    public IReadOnlyList<Prompt> Prompts { get; init; } = [];
}

public sealed record McpResourcesListResponseBody
{
    public IReadOnlyList<Resource> Resources { get; init; } = [];
    public IReadOnlyList<ResourceTemplate> ResourceTemplates { get; init; } = [];
}

public sealed record McpToolCallRequestBody
{
    public string? ToolId { get; init; }
    public string? ToolName { get; init; }
    public string PayloadJson { get; init; } = "{}";
}

public sealed record McpPromptGetRequestBody
{
    public string? PromptId { get; init; }
    public string? PromptName { get; init; }
    public IReadOnlyDictionary<string, JsonElement>? Arguments { get; init; }
}

public sealed record McpPromptGetResponseBody
{
    public string PromptId { get; init; } = string.Empty;
    public string PromptName { get; init; } = string.Empty;
    public GetPromptResult Result { get; init; } = new();
}

public sealed record McpResourceReadRequestBody
{
    public string? ResourceId { get; init; }
    public string? ResourceName { get; init; }
    public string Uri { get; init; } = string.Empty;
}

public sealed record McpResourceReadResponseBody
{
    public string ResourceId { get; init; } = string.Empty;
    public string ResourceName { get; init; } = string.Empty;
    public ReadResourceResult Result { get; init; } = new();
}

public sealed record McpToolCallResponseBody
{
    public string ToolId { get; init; } = string.Empty;
    public string ToolName { get; init; } = string.Empty;
    public ExecutionState State { get; init; } = ExecutionState.Completed;
    public string Detail { get; init; } = string.Empty;
    public CallToolResult Result { get; init; } = new();
}

public sealed record McpExecutionResponseBody
{
    public McpExecutionSnapshot Execution { get; init; } = new();
}

public sealed record McpShutdownResponseBody
{
    public string Shutdown { get; } = "detached";
}

public sealed record McpToolsChangedEventBody
{
    public DateTime ChangedAtUtc { get; } = DateTime.UtcNow;
}

public sealed record McpErrorBody
{
    public string Code { get; init; } = "bridge.error";
    public string Message { get; init; } = string.Empty;
    public string? Details { get; init; }
}
