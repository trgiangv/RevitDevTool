using System.IO;
using RevitDevTool.Execution.Models;
namespace RevitDevTool.Mcp.Schemas;

public sealed class McpToolDefinition
{
    public string ToolId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string InputSchemaJson { get; set; } = "{}";
    public string? OutputSchemaJson { get; set; }
    public string? AnnotationsJson { get; set; }
    public string? MetaJson { get; set; }
    public bool StructuredOutput { get; set; } = true;
    public ExecutionMode SourceKind { get; set; } = ExecutionMode.Python;
    public string ContainerType { get; set; } = string.Empty;
    public string MethodName { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public string SourceAddress { get; set; } = string.Empty;
    public string GroupKey { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;

    public void EnsureIdentity()
    {
        if (string.IsNullOrWhiteSpace(SourceAddress))
            SourceAddress = BuildFallbackSourceAddress(SourceKind, SourcePath, ContainerType, MethodName);

        if (string.IsNullOrWhiteSpace(GroupKey))
            GroupKey = SourcePath?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(GroupName))
            GroupName = BuildFallbackGroupName(SourceKind, SourcePath, ContainerType);

        if (!string.IsNullOrWhiteSpace(ToolId))
            return;

        ToolId = CreateToolId(Name, SourceAddress);
    }

    public static string CreateToolId(string? name, string? sourceAddress)
    {
        return $"{NormalizeIdentitySegment(name)}_[{NormalizeIdentitySegment(sourceAddress)}]";
    }

    private static string BuildFallbackSourceAddress(
        ExecutionMode sourceKind,
        string? sourcePath,
        string? containerType,
        string? methodName)
    {
        var location = sourceKind switch
        {
            ExecutionMode.Assembly => Path.GetFileName(sourcePath) ?? string.Empty,
            _ => Path.GetFileNameWithoutExtension(sourcePath) ?? string.Empty
        };
        var container = containerType?.Trim() ?? string.Empty;
        var method = methodName?.Trim() ?? string.Empty;

        return string.Join(":", new[]
        {
            location,
            string.Join(".", new[] { container, method }.Where(value => !string.IsNullOrWhiteSpace(value)))
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string BuildFallbackGroupName(ExecutionMode sourceKind, string? sourcePath, string? containerType)
    {
        var sourceName = sourceKind == ExecutionMode.Assembly
            ? Path.GetFileName(sourcePath)
            : Path.GetFileName(Path.GetDirectoryName(sourcePath ?? string.Empty) ?? string.Empty);

        return !string.IsNullOrWhiteSpace(sourceName)
            ? sourceName
            : containerType?.Trim() ?? "MCP Toolset";
    }

    private static string NormalizeIdentitySegment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "unknown";

        return value!.Trim()
            .Replace('\\', '/')
            .Replace(' ', '-')
            .Replace("\r", string.Empty)
            .Replace("\n", string.Empty);
    }

}

public sealed record McpProgressUpdate
{
    public string Stage { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

public sealed record McpToolExecutionMetadata
{
    public string ExecutionId { get; init; } = Guid.NewGuid().ToString("N");
    public string ToolId { get; init; } = string.Empty;
    public string ToolName { get; init; } = string.Empty;
    public long DurationMs { get; init; }
    public DateTime StartedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime CompletedAtUtc { get; init; } = DateTime.UtcNow;
}

public sealed record McpExecutionSnapshot
{
    public string ExecutionId { get; init; } = string.Empty;
    public string ToolId { get; init; } = string.Empty;
    public string ToolName { get; init; } = string.Empty;
    public string State { get; init; } = McpExecutionStates.Queued;
    public string Message { get; init; } = string.Empty;
    public string ResultKind { get; init; } = McpResultKinds.Empty;
    public bool CanCancel { get; init; }
    public DateTime StartedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; init; }
    public McpException? Error { get; init; }
    public IReadOnlyList<McpProgressUpdate> ProgressUpdates { get; init; } = [];
}

public sealed record McpToolExecutionResult
{
    public bool Success { get; init; }
    public bool IsCancelled { get; init; }
    public string Message { get; init; } = string.Empty;
    public string ResultKind { get; init; } = McpResultKinds.Json;
    public string PayloadJson { get; init; } = "{}";
    public McpException? Error { get; init; }
    public McpToolExecutionMetadata? Metadata { get; init; }
    public IReadOnlyList<McpProgressUpdate> ProgressUpdates { get; init; } = [];

    public static McpToolExecutionResult Succeeded(
        string payloadJson,
        string message,
        string resultKind = McpResultKinds.Json,
        McpToolExecutionMetadata? metadata = null,
        IReadOnlyList<McpProgressUpdate>? progressUpdates = null)
        => new()
        {
            Success = true,
            PayloadJson = payloadJson,
            Message = message,
            ResultKind = resultKind,
            Metadata = metadata,
            ProgressUpdates = progressUpdates ?? []
        };

    public static McpToolExecutionResult Failed(
        string code,
        string message,
        string? details = null,
        McpToolExecutionMetadata? metadata = null,
        IReadOnlyList<McpProgressUpdate>? progressUpdates = null)
        => new()
        {
            Success = false,
            Message = message,
            ResultKind = McpResultKinds.Json,
            PayloadJson = "{}",
            Metadata = metadata,
            ProgressUpdates = progressUpdates ?? [],
            Error = new McpException { Code = code, Message = message, Details = details }
        };

    public static McpToolExecutionResult Cancelled(
        string message,
        McpToolExecutionMetadata? metadata = null,
        IReadOnlyList<McpProgressUpdate>? progressUpdates = null)
        => new()
        {
            Success = false,
            IsCancelled = true,
            Message = message,
            ResultKind = McpResultKinds.Json,
            PayloadJson = "{}",
            Metadata = metadata,
            ProgressUpdates = progressUpdates ?? [],
            Error = new McpException { Code = "tool.cancelled", Message = message }
        };
}
