using System.ComponentModel;
using System.Diagnostics;
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
        var total = request.NodeIds.Count + 1;
        var completed = 0;
        var completedNodeIds = new HashSet<string>(StringComparer.Ordinal);
        var caseSequence = 0;
        var caseEvents = TryGetCaseEventContext(server, requestContext, out var progressToken);
        var stopwatch = Stopwatch.StartNew();
        PytestRunResponse? response = null;
        string? infrastructureCode = null;
        logger.LogInformation(
            "Pytest MCP request started. NodeCount={NodeCount} ProgressToken={ProgressToken}",
            request.NodeIds.Count,
            progressToken);
        progress?.Report(new ProgressNotificationValue
        {
            Progress = completed,
            Total = total,
            Message = "Preparing pytest dependencies."
        });

        try
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await dependencyService.PrepareRunAsync(request, cancellationToken).ConfigureAwait(false);
                completed++;
                progress?.Report(new ProgressNotificationValue
                {
                    Progress = completed,
                    Total = total,
                    Message = "Pytest dependencies are ready."
                });
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException ex)
            {
                logger.LogInformation(ex, "Host shut down while preparing pytest MCP run dependencies.");
                infrastructureCode = PytestMcpErrorCodes.HostShuttingDown;
                return InfrastructureError(infrastructureCode, "The host is shutting down.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to prepare pytest MCP run dependencies.");
                infrastructureCode = PytestMcpErrorCodes.DependencyPreparationFailed;
                return InfrastructureError(infrastructureCode, "Pytest dependencies could not be prepared.");
            }

            var runnerStarted = 0;
            var runnerCompletion = new TaskCompletionSource<PytestRunResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            var previousMode = ExecutionGuardContext.Mode;
            try
            {
                ExecutionGuardContext.Mode = ExecutionGuardMode.Suppress;
                response = await hostContext.ExecuteAsync(
                    () =>
                    {
                        Interlocked.Exchange(ref runnerStarted, 1);
                        try
                        {
                            var result = executionService.Run(
                                request,
                                resultJson => completed = PublishCaseResult(
                                    resultJson,
                                    progress,
                                    total,
                                    completed,
                                    completedNodeIds,
                                    ++caseSequence,
                                    caseEvents,
                                    server,
                                    progressToken,
                                    cancellationToken),
                                CancellationToken.None);
                            runnerCompletion.TrySetResult(result);
                            return result;
                        }
                        catch (Exception ex)
                        {
                            runnerCompletion.TrySetException(ex);
                            throw;
                        }
                    },
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                if (Volatile.Read(ref runnerStarted) == 0)
                    throw;

                response = await runnerCompletion.Task.ConfigureAwait(false);
            }
            catch (OperationCanceledException ex)
            {
                logger.LogInformation(ex, "Host shut down while executing pytest MCP run.");
                infrastructureCode = PytestMcpErrorCodes.HostShuttingDown;
                return InfrastructureError(infrastructureCode, "The host is shutting down.");
            }
            catch (ObjectDisposedException ex)
            {
                logger.LogInformation(ex, "Host shut down while executing pytest MCP run.");
                infrastructureCode = PytestMcpErrorCodes.HostShuttingDown;
                return InfrastructureError(infrastructureCode, "The host is shutting down.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Pytest MCP {FailurePhase} failed.", runnerStarted != 0 ? "runner" : "host context");
                infrastructureCode = runnerStarted != 0
                    ? PytestMcpErrorCodes.RunnerFailed
                    : PytestMcpErrorCodes.HostContextUnavailable;
                return InfrastructureError(
                    infrastructureCode,
                    runnerStarted != 0 ? "Pytest execution failed in the host." : "The host context is unavailable.");
            }
            finally
            {
                ExecutionGuardContext.Mode = previousMode;
            }

            cancellationToken.ThrowIfCancellationRequested();

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
                infrastructureCode = PytestMcpErrorCodes.SerializationFailed;
                return InfrastructureError(infrastructureCode, "The pytest result could not be serialized.");
            }
        }
        finally
        {
            logger.LogInformation(
                "Pytest MCP request ended. DurationMs={DurationMs} NodeCount={NodeCount} ExitCode={ExitCode} Passed={Passed} Failed={Failed} Errors={Errors} Cancelled={Cancelled} InfrastructureCode={InfrastructureCode} ProgressToken={ProgressToken}",
                stopwatch.ElapsedMilliseconds,
                request.NodeIds.Count,
                response?.ExitCode,
                response?.Summary.Passed,
                response?.Summary.Failed,
                response?.Summary.Errors,
                cancellationToken.IsCancellationRequested,
                infrastructureCode,
                progressToken);
        }
    }

    private static CallToolResult InfrastructureError(string code, string message) => new()
    {
        IsError = true,
        Content = [new TextContentBlock { Text = message }],
        StructuredContent = JsonSerializer.SerializeToElement(new { status = code })
    };

    private static int PublishCaseResult(
        string resultJson,
        IProgress<ProgressNotificationValue>? progress,
        int total,
        int completed,
        ISet<string> completedNodeIds,
        int caseSequence,
        bool caseEvents,
        McpServer? server,
        ProgressToken progressToken,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return completed;

        PytestCaseResult? caseResult;
        try
        {
            caseResult = JsonSerializer.Deserialize<PytestCaseResult>(resultJson);
        }
        catch (JsonException)
        {
            return completed;
        }

        if (caseResult is null || cancellationToken.IsCancellationRequested)
            return completed;

        if (CompletesNode(caseResult) && completedNodeIds.Add(caseResult.NodeId))
        {
            completed++;
            progress?.Report(new ProgressNotificationValue
            {
                Progress = completed,
                Total = total,
                Message = caseResult.NodeId
            });
        }

        if (!caseEvents || server is null || cancellationToken.IsCancellationRequested)
            return completed;

        server.SendNotificationAsync(
                "notifications/devtools/pytest/case",
                new PytestCaseEvent(progressToken, caseSequence, caseResult),
                cancellationToken: cancellationToken)
            .GetAwaiter()
            .GetResult();

        return completed;
    }

    private static bool CompletesNode(PytestCaseResult caseResult) =>
        string.Equals(caseResult.Phase, "call", StringComparison.Ordinal)
        || (string.Equals(caseResult.Phase, "setup", StringComparison.Ordinal)
            && (string.Equals(caseResult.Outcome, "failed", StringComparison.Ordinal)
                || string.Equals(caseResult.Outcome, "error", StringComparison.Ordinal)
                || string.Equals(caseResult.Outcome, "skipped", StringComparison.Ordinal)));

    private static bool TryGetCaseEventContext(
        McpServer? server,
        RequestContext<CallToolRequestParams>? requestContext,
        out ProgressToken progressToken)
    {
        progressToken = default;
        if (server?.ClientCapabilities?.Experimental is null
            || requestContext?.Params?.ProgressToken is not { } requestProgressToken)
        {
            return false;
        }

        progressToken = requestProgressToken;
        var experimental = JsonSerializer.SerializeToElement(server.ClientCapabilities.Experimental);
        return experimental.TryGetProperty("devtools", out var devtools)
               && devtools.TryGetProperty("pytest", out var pytest)
               && pytest.TryGetProperty("caseEvents", out var caseEvents)
               && caseEvents.TryGetProperty("version", out var version)
               && version.ValueKind == JsonValueKind.String
               && string.Equals(version.GetString(), "1", StringComparison.Ordinal);
    }
}
