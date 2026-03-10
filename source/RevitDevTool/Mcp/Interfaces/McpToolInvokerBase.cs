using System.Text.Json;
using RevitDevTool.Contracts;
namespace RevitDevTool.Mcp.Interfaces;

public abstract class McpToolInvokerBase : IMcpToolInvoker
{
    public abstract bool CanHandle(ExecutionMode executionMode);

    public async Task<McpToolExecutionResult> ExecuteAsync(
        McpToolDefinition definition,
        string? payloadJson,
        IProgress<McpProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedPayload = NormalizePayload(payloadJson);
            definition.EnsureIdentity();
            cancellationToken.ThrowIfCancellationRequested();
            return await ExecuteCoreAsync(definition, normalizedPayload, progress, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return McpToolExecutionResult.Cancelled($"Tool '{definition.Name}' was cancelled.");
        }
        catch (Exception ex)
        {
            return McpToolExecutionResult.Failed("tool.invoke_failed", ex.Message, ex.StackTrace);
        }
    }

    protected abstract Task<McpToolExecutionResult> ExecuteCoreAsync(
        McpToolDefinition definition,
        string normalizedPayload,
        IProgress<McpProgressUpdate>? progress,
        CancellationToken cancellationToken);

    private static string NormalizePayload(string? payloadJson)
    {
        using var doc = JsonDocument.Parse(
            // ReSharper disable once RedundantSuppressNullableWarningExpression
            string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson!);
        return doc.RootElement.ValueKind != JsonValueKind.Object 
            ? throw new JsonException("Tool payload must be a JSON object.") 
            : doc.RootElement.GetRawText();
    }
}
