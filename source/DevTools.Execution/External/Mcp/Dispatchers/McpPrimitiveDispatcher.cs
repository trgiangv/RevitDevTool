using DevTools.Mcp.Core.Invocation;
using DevTools.Mcp.Core.Models;
using DevTools.Mcp.Core.Results;
using DevTools.Telemetry;
using ModelContextProtocol.Protocol;
using CoreMcpErrorCode = DevTools.Mcp.Core.Results.McpErrorCode;

namespace DevTools.Execution.External.Mcp.Dispatchers;

/// <summary>Routes host MCP primitives; source backends own invocation details and caches.</summary>
public sealed class McpPrimitiveDispatcher(
    IEnumerable<IMcpPrimitiveBackend> backends,
    ITelemetry telemetry) : IMcpPrimitiveDispatcher
{
    private readonly IReadOnlyDictionary<ExecutionMode, IMcpPrimitiveBackend> _backends =
        backends.ToDictionary(backend => backend.SourceKind);

    public async Task<McpResult<McpInvocationResponse>> DispatchToolAsync(
        McpRegisteredTool tool,
        CallToolRequestParams request,
        IHostContextExecutor hostContext,
        CancellationToken ct = default)
    {
        telemetry.RecordMcpInvocation(tool.Binding.SourceKind.ToString());
        if (!_backends.TryGetValue(tool.Binding.SourceKind, out var backend))
        {
            return McpResult<McpInvocationResponse>.Failure(new McpError(
                CoreMcpErrorCode.ExecutionFailed,
                $"Unsupported MCP tool source '{tool.Binding.SourceKind}'.",
                []));
        }

        try
        {
            return await backend.InvokeToolAsync(tool, request, hostContext, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (TryMapInputRequired(ex) is { } mapped)
        {
            return mapped;
        }
        catch (Exception ex)
        {
            if (TelemetryReporting.ShouldReportCriticalException(ex))
            {
                telemetry.RecordCriticalException(
                    ex,
                    TelemetryKeys.Feature.Mcp,
                    new Dictionary<string, string>
                    {
                        [TelemetryKeys.Tag.Provider] = tool.Binding.SourceKind.ToString()
                    });
            }

            return McpResult<McpInvocationResponse>.Failure(
                new McpError(CoreMcpErrorCode.ExecutionFailed, ex.Message, []));
        }
    }

    public ReadResourceResult ReadResource(
        McpRegisteredResource resource,
        string uri,
        CancellationToken ct = default)
    {
        if (!_backends.TryGetValue(resource.Binding.SourceKind, out var backend))
            throw new InvalidOperationException($"Unsupported MCP resource source '{resource.Binding.SourceKind}'.");
        return backend.ReadResource(resource, uri, ct);
    }

    public void ClearCaches()
    {
        foreach (var backend in _backends.Values)
            backend.ClearCaches();
    }

    private static McpResult<McpInvocationResponse>? TryMapInputRequired(Exception exception) =>
        exception switch
        {
            InputRequiredException host => McpResult<McpInvocationResponse>.Success(
                ToolsetMrtrBridge.ToInputRequiredResponse(host)),
            _ when ToolsetMrtrBridge.IsIsolatedInputRequired(exception) => McpResult<McpInvocationResponse>.Success(
                ToolsetMrtrBridge.ToInputRequiredResponse(ToolsetMrtrBridge.ToHostException(exception))),
            _ => null
        };
}
