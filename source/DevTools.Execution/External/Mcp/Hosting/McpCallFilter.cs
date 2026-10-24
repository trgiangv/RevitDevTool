using DevTools.Execution.External.Mcp.Connections;
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
    private const string BuiltInContainerType = "BuiltIn";

    public void Configure(McpServerOptions options)
    {
        options.Filters.Request.CallToolFilters.Add(next => async (request, cancellationToken) =>
        {
            var toolName = request.Params.Name;
            catalogStore.TryGetTool(null, toolName, out var registered);
            var isBuiltIn = registered?.Binding.ContainerType == BuiltInContainerType;
            var sourceKind = isBuiltIn ? BuiltInContainerType : registered?.Binding.SourceKind.ToString() ?? "unknown";
            using var operation = telemetry.StartOperation(TelemetryKeys.Operation.Mcp, sourceKind);

            using var scope = executionTracker.BeginExecution(toolName);
            executionTracker.MarkRunning(scope);
            ExecutionGuardContext.Mode = ExecutionGuardMode.Suppress;

            try
            {
                var callResult = await next(request, cancellationToken).ConfigureAwait(false);
                if (callResult.IsError == true)
                    operation.Fail();
                else
                    operation.Complete();

                Complete(scope, registered, toolName, callResult);
                return callResult;
            }
            catch (Exception ex)
            {
                operation.Fail(isBuiltIn ? ex : TelemetryReporter.MarkExternal(ex));
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
