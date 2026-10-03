using DevTools.Execution.Abstractions;
using DevTools.Mcp;
using DevTools.Execution.External.Mcp.Connections;
using DevTools.Mcp.Core.Utils;
using DevTools.Telemetry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Execution.External.Mcp.Hosting;

/// <summary>
/// Cross-cutting tool-call behavior: guard mode, execution tracking, and failure mapping.
/// The SDK collection invokes the tool; this filter does not choose a backend.
/// </summary>
internal sealed class McpCallFilter(
    McpCatalogStore catalogStore,
    IMcpExecutionTracker executionTracker,
    ITelemetry telemetry,
    ILogger<McpCallFilter> logger) : IConfigureOptions<McpServerOptions>
{
    public void Configure(McpServerOptions options)
    {
        options.Filters.Request.CallToolFilters.Add(next => async (request, cancellationToken) =>
        {
            var toolName = request.Params?.Name ?? string.Empty;
            catalogStore.TryGetTool(null, toolName, out var registered);
            var sourceKind = registered?.Binding.SourceKind.ToString() ?? "unknown";
            telemetry.RecordMcpInvocation(sourceKind);

            using var scope = executionTracker.BeginExecution(toolName);
            executionTracker.MarkRunning(scope);
            ExecutionGuardContext.Mode = ExecutionGuardMode.Suppress;

            try
            {
                var callResult = await next(request, cancellationToken).ConfigureAwait(false);
                Complete(scope, registered, toolName, callResult);
                return callResult;
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
                            [TelemetryKeys.Tag.Provider] = sourceKind
                        });
                }

                logger.LogError(ex, "Tool '{ToolName}' failed", toolName);
                executionTracker.Complete(scope, ExecutionState.Failed, ex.Message);
                return ToToolError(ex);
            }
        });
    }

    private void Complete(
        IDisposable scope,
        DevTools.Mcp.Core.Models.RegisteredTool? registered,
        string toolName,
        CallToolResult callResult)
    {
        if (callResult.IsError == true)
        {
            var message = callResult.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? $"Tool '{toolName}' failed.";
            executionTracker.Complete(scope, ExecutionState.Failed, message);
            return;
        }

        executionTracker.Complete(scope, ExecutionState.Completed, $"Completed '{toolName}'.");
        if (registered is not null)
            executionTracker.RecordCall(registered.Id, toolName);
    }

    internal static CallToolResult ToToolError(Exception ex) => ToolHelpers.ErrorResult(ex.Message);
}
