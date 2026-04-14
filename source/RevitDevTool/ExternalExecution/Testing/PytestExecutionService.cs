using System.IO;
using System.Text.Json;
using Python.Runtime;
using RevitDevTool.Execution.Providers.Python;
// ReSharper disable ConditionalAccessQualifierIsNonNullableAccordingToAPIContract

namespace RevitDevTool.ExternalExecution.Testing;

public sealed class PytestExecutionService(PythonExecutor executor)
{
    private static readonly JsonSerializerOptions RequestOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly PytestSummary EmptySummary = new(0, 0, 0, 1, 0, 0);

    public static bool TryParseDiscoverRequest(JsonElement? @params, out PytestDiscoverRequest? request, out string? error)
    {
        request = null;

        if (@params is null)
        {
            error = "Pytest discover request is required.";
            return false;
        }

        try
        {
            request = JsonSerializer.Deserialize<PytestDiscoverRequest>(@params.Value.GetRawText(), RequestOptions);
        }
        catch (Exception ex)
        {
            error = $"Invalid pytest discover request: {ex.Message}";
            return false;
        }

        if (request is null || string.IsNullOrWhiteSpace(request.TestRoot))
        {
            error = "test_root is required.";
            return false;
        }

        var workspaceRoot = PytestPathResolver.ResolveWorkspaceRoot(request.WorkspaceRoot, request.TestRoot);
        var pytestArgs = request.PytestArgs?.Where(arg => !string.IsNullOrWhiteSpace(arg)).ToList() ?? [];
        request = new PytestDiscoverRequest(workspaceRoot, PytestPathResolver.ResolvePath(request.TestRoot, workspaceRoot), pytestArgs);
        error = null;
        return true;
    }

    public static bool TryParseRunRequest(JsonElement? @params, out PytestRunRequest? request, out string? error)
    {
        request = null;

        if (@params is null)
        {
            error = "Pytest run request is required.";
            return false;
        }

        try
        {
            request = JsonSerializer.Deserialize<PytestRunRequest>(@params.Value.GetRawText(), RequestOptions);
        }
        catch (Exception ex)
        {
            error = $"Invalid pytest run request: {ex.Message}";
            return false;
        }

        if (request is null || string.IsNullOrWhiteSpace(request.TestRoot))
        {
            error = "test_root is required.";
            return false;
        }

        var nodeIds = request.NodeIds?.Where(nodeId => !string.IsNullOrWhiteSpace(nodeId)).ToList() ?? [];
        if (nodeIds.Count == 0)
        {
            error = "At least one nodeid is required.";
            return false;
        }

        var workspaceRoot = PytestPathResolver.ResolveWorkspaceRoot(request.WorkspaceRoot, request.TestRoot);
        var pytestArgs = request.PytestArgs?.Where(arg => !string.IsNullOrWhiteSpace(arg)).ToList() ?? [];
        request = new PytestRunRequest(workspaceRoot, PytestPathResolver.ResolvePath(request.TestRoot, workspaceRoot), nodeIds, pytestArgs);
        error = null;
        return true;
    }

    public PytestDiscoverResponse Discover(PytestDiscoverRequest request)
    {
        var runnerRequest = new PytestRunnerRequest(
            request.WorkspaceRoot,
            request.TestRoot,
            [],
            request.PytestArgs,
            true);

        return Execute<PytestDiscoverResponse>(runnerRequest, request.TestRoot);
    }

    public PytestRunResponse Run(PytestRunRequest request)
    {
        var runnerRequest = new PytestRunnerRequest(
            request.WorkspaceRoot,
            request.TestRoot,
            request.NodeIds,
            request.PytestArgs,
            false);

        return Execute<PytestRunResponse>(runnerRequest, request.TestRoot);
    }

    public static PytestRunResponse Error(string phase, string message, string? details = null)
    {
        return new PytestRunResponse(
            1,
            EmptySummary,
            [],
            [new PytestCollectionError(string.Empty, string.Empty, $"[{phase}] {message}", details ?? string.Empty)],
            string.Empty);
    }

    private T Execute<T>(PytestRunnerRequest request, string anchorPath)
    {
        var rootFolder = Directory.Exists(request.TestRoot)
            ? request.TestRoot
            : Path.GetDirectoryName(request.TestRoot) ?? request.WorkspaceRoot;
        var anchorFile = ResolveAnchorFile(anchorPath, rootFolder);

        return executor.Execute(
            anchorFile,
            rootFolder,
            scope =>
            {
                scope.Set(PythonInstances.PytestRequestJson, new PyString(JsonSerializer.Serialize(request)));
                scope.Exec(PythonEmbedded.PytestRunnerScript);

                var resultJson = scope.Get(PythonInstances.ResultJson).As<string>();
                var response = JsonSerializer.Deserialize<T>(resultJson, RequestOptions);
                return response ?? throw new InvalidOperationException("Pytest runner returned an empty response.");
            });
    }

    private static string ResolveAnchorFile(string anchorPath, string rootFolder)
    {
        return File.Exists(anchorPath) 
            ? anchorPath 
            : Path.Combine(rootFolder, "__pytest__.py");
    }
}
