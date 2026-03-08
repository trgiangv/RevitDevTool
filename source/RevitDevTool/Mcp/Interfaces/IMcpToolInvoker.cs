using RevitDevTool.Execution.Models;
using RevitDevTool.Mcp.Schemas;
namespace RevitDevTool.Mcp.Interfaces;

public interface IMcpToolInvoker
{
    bool CanHandle(ExecutionMode executionMode);

    Task<McpToolExecutionResult> ExecuteAsync(
        McpToolDefinition definition,
        string? payloadJson,
        IProgress<McpProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default);
}
