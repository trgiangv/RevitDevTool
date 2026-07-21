using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ZLogger;

namespace DevTools.Mcp.Dispatch;

/// <summary>Marker type for DI logger resolution (static dispatch helpers cannot be type arguments).</summary>
internal sealed class McpExecutionDispatchLogger;

internal static class McpExecutionDispatch
{
    internal static async ValueTask<CallToolResult> InvokeToolAsync(
        RequestContext<CallToolRequestParams> request,
        string toolName,
        string? toolId,
        Func<ValueTask<CallToolResult>> invoke,
        CancellationToken cancellationToken)
    {
        // Correlation id: Guid.NewGuid().ToString("N") — 32-char hex without dashes (T2.3/T2.4 scheme).
        var correlationId = Guid.NewGuid().ToString("N");
        var tracker = ResolveTracker(request);
        var logger = ResolveLogger(request);
        var host = ResolveHostName(request);

        IDisposable? scope = null;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            scope = tracker?.BeginExecution(toolName);
            if (scope is not null)
                tracker?.MarkRunning(scope);

            tracker?.RecordCall(toolId ?? toolName, toolName);

            var result = await invoke().ConfigureAwait(false);
            stopwatch.Stop();

            var executionResult = ToExecutionResult(result);
            if (scope is not null)
                tracker?.Complete(scope, executionResult);

            LogInvocation(logger, correlationId, toolName, host, stopwatch.ElapsedMilliseconds, executionResult.State);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            if (scope is not null)
                tracker?.Complete(scope, McpToolExecutionResult.Cancelled("MCP tool invocation was cancelled."));

            LogInvocation(logger, correlationId, toolName, host, stopwatch.ElapsedMilliseconds, ExecutionState.Cancelled);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            if (scope is not null)
            {
                tracker?.Complete(
                    scope,
                    McpToolExecutionResult.Failed(McpExecutionErrorCodes.ToolInvokeFailed, ex.Message));
            }

            LogInvocation(logger, correlationId, toolName, host, stopwatch.ElapsedMilliseconds, ExecutionState.Failed);
            throw;
        }
    }

    private static IMcpExecutionTracker? ResolveTracker(RequestContext<CallToolRequestParams> request) =>
        request.Services?.GetService<IMcpExecutionTracker>();

    private static ILogger<McpExecutionDispatchLogger>? ResolveLogger(RequestContext<CallToolRequestParams> request)
    {
        var services = request.Services;
        if (services is null)
            return null;

        return services.GetService<ILogger<McpExecutionDispatchLogger>>()
            ?? services.GetService<ILoggerFactory>()?.CreateLogger<McpExecutionDispatchLogger>();
    }

    private static string ResolveHostName(RequestContext<CallToolRequestParams> request) =>
        request.Services?.GetService<IMcpHostIdentity>()?.HostName ?? "unknown";

    private static McpToolExecutionResult ToExecutionResult(CallToolResult result) =>
        result.IsError == true
            ? McpToolExecutionResult.Failed(
                McpExecutionErrorCodes.ToolFailed,
                "MCP tool returned an error result.")
            : McpToolExecutionResult.Completed(result, "MCP tool completed.");

    private static void LogInvocation(
        ILogger<McpExecutionDispatchLogger>? logger,
        string correlationId,
        string toolName,
        string host,
        long durationMs,
        ExecutionState state)
    {
        if (logger is null)
            return;

        var success = state == ExecutionState.Completed;
        // Metadata only: correlation, tool, host, duration, success — never request/response bodies (T2.3).
        logger.ZLogInformation(
            $"MCP tool {state} correlationId={correlationId} tool={toolName} host={host} durationMs={durationMs} success={success}");
    }
}
