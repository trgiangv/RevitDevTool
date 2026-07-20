using System.ComponentModel;
using System.Text.Json;
using DevTools.Execution.External.Testing;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Execution.External.Mcp.BuiltIn;

/// <summary>Executes locally collected pytest node IDs inside the host process.</summary>
public sealed class PytestRunTool(
    IHostContextExecutor hostContext,
    PytestDependencyService dependencyService,
    PytestExecutionService executionService,
    ILogger<PytestRunTool> logger) : IBuiltInMcpTool
{
#pragma warning disable MCPEXP001
    public McpServerTool Primitive
    {
        get
        {
            var primitive = McpServerTool.Create(typeof(PytestRunTool).GetMethod(nameof(RunAsync))!, this);
            primitive.ProtocolTool.Execution = new ToolExecution { TaskSupport = ToolTaskSupport.Optional };
            return primitive;
        }
    }
#pragma warning restore MCPEXP001

    [McpServerTool(Name = "pytest_run")]
    [Description("Execute locally collected pytest node IDs inside this host process.")]
    public async Task<CallToolResult> RunAsync(
        string workspace_root,
        string test_root,
        string[] nodeids,
        string[] pytest_args,
        IProgress<ProgressNotificationValue> progress,
        McpServer server,
        RequestContext<CallToolRequestParams> requestContext,
        CancellationToken cancellationToken = default)
    {
        var rawRequest = new PytestRunRequest(workspace_root, test_root, nodeids, pytest_args);
        if (!PytestExecutionService.ValidateRunRequest(rawRequest, out var validationError))
        {
            logger.LogWarning("Rejected invalid pytest MCP request: {ValidationError}", validationError);
            return InfrastructureError(PytestMcpErrorCodes.InvalidInput, "The pytest run request is invalid.");
        }

        var request = PytestExecutionService.NormalizeRunRequest(rawRequest);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await dependencyService.PrepareRunAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            logger.LogInformation(ex, "Host shut down while preparing pytest MCP run dependencies.");
            return InfrastructureError(PytestMcpErrorCodes.HostShuttingDown, "The host is shutting down.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to prepare pytest MCP run dependencies.");
            return InfrastructureError(PytestMcpErrorCodes.DependencyPreparationFailed, "Pytest dependencies could not be prepared.");
        }

        PytestRunResponse response;
        var runnerStarted = false;
        var previousMode = ExecutionGuardContext.Mode;
        try
        {
            ExecutionGuardContext.Mode = ExecutionGuardMode.Suppress;
            response = await hostContext.ExecuteAsync(
                () =>
                {
                    runnerStarted = true;
                    return executionService.Run(request);
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            logger.LogInformation(ex, "Host shut down while executing pytest MCP run.");
            return InfrastructureError(PytestMcpErrorCodes.HostShuttingDown, "The host is shutting down.");
        }
        catch (ObjectDisposedException ex)
        {
            logger.LogInformation(ex, "Host shut down while executing pytest MCP run.");
            return InfrastructureError(PytestMcpErrorCodes.HostShuttingDown, "The host is shutting down.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Pytest MCP {FailurePhase} failed.", runnerStarted ? "runner" : "host context");
            return InfrastructureError(
                runnerStarted ? PytestMcpErrorCodes.RunnerFailed : PytestMcpErrorCodes.HostContextUnavailable,
                runnerStarted ? "Pytest execution failed in the host." : "The host context is unavailable.");
        }
        finally
        {
            ExecutionGuardContext.Mode = previousMode;
        }

        try
        {
            var structuredContent = JsonSerializer.SerializeToElement(response);
            return new CallToolResult
            {
                Content = [new TextContentBlock
                {
                    Text = $"pytest exit {response.ExitCode}: {response.Summary.Passed} passed, {response.Summary.Failed} failed, {response.Summary.Errors} errors"
                }],
                StructuredContent = structuredContent
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to serialize pytest MCP result.");
            return InfrastructureError(PytestMcpErrorCodes.SerializationFailed, "The pytest result could not be serialized.");
        }
    }

    private static CallToolResult InfrastructureError(string code, string message) => new()
    {
        IsError = true,
        Content = [new TextContentBlock { Text = message }],
        StructuredContent = JsonSerializer.SerializeToElement(new { status = code })
    };
}
