using RevitDevTool.Contracts;
using RevitDevTool.Mcp.Interfaces;
namespace RevitDevTool.Mcp;

public sealed class McpToolExecutionDispatcher(IEnumerable<IMcpToolInvoker> invokers)
{
    private readonly IReadOnlyList<IMcpToolInvoker> _invokers = invokers.ToList();

    public async Task<McpToolExecutionResult> DispatchAsync(
        McpToolDefinition definition,
        string? payloadJson,
        IProgress<McpProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var invoker = _invokers.FirstOrDefault(item => item.CanHandle(definition.SourceKind));
        return invoker is null 
            ? BuildUnsupportedSourceError(definition.SourceKind) 
            : await invoker.ExecuteAsync(definition, payloadJson, progress, cancellationToken).ConfigureAwait(false);
    }

    private static McpToolExecutionResult BuildUnsupportedSourceError(ExecutionMode executionMode)
    {
        return McpToolExecutionResult.Failed(
            "tool.unknown_source_kind",
            $"Unknown or unsupported MCP tool execution: '{executionMode}'");
    }
}
