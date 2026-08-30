using System.IO;
using System.Text.Json;
using DevTools.Execution.Providers;
using DevTools.Execution.Providers.Python;

namespace DevTools.Execution.External.Testing;

public sealed class IpyTestExecutionService(IScriptExecutionStrategyFactory strategyFactory)
{
    private const string RequestEnvVar = "IPYTEST_REQUEST";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static bool TryParseRunRequest(JsonElement? @params, out PytestRunRequest? request, out string? error)
        => PytestExecutionService.TryParseRunRequest(@params, out request, out error);

    public static PytestRunResponse Error(string phase, string message, string? details = null)
        => PytestExecutionService.Error(phase, message, details);

    public async Task<PytestRunResponse> RunAsync(PytestRunRequest request, CancellationToken cancellationToken = default)
    {
        var groups = GroupNodeIds(request.NodeIds, request.WorkspaceRoot);
        if (groups.Count == 0)
            return Error("prepare", "No IronPython test files in nodeids.");

        var results = new List<PytestCaseResult>();
        var collectionErrors = new List<PytestCollectionError>();
        var engine = "";

        foreach (var (testPath, nodeIds) in groups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(testPath))
            {
                collectionErrors.Add(new PytestCollectionError(
                    nodeIds[0],
                    testPath,
                    $"IronPython test file not found: {testPath}",
                    string.Empty));
                continue;
            }

            var fileResult = await RunFileAsync(testPath, nodeIds, request.WorkspaceRoot, cancellationToken)
                .ConfigureAwait(false);
            if (!string.IsNullOrEmpty(fileResult.Engine))
                engine = fileResult.Engine;
            results.AddRange(fileResult.Results);
            collectionErrors.AddRange(fileResult.CollectionErrors);
        }

        var failed = results.Count(r => r.Outcome is "failed" or "error") + collectionErrors.Count;
        var passed = results.Count(r => r.Outcome == "passed");
        var skipped = results.Count(r => r.Outcome == "skipped");
        var summary = new PytestSummary(passed, failed, skipped, collectionErrors.Count, 0, 0);
        return new PytestRunResponse(
            failed == 0 && collectionErrors.Count == 0 ? 0 : 1,
            summary,
            results,
            collectionErrors,
            request.WorkspaceRoot,
            engine);
    }

    public static Dictionary<string, List<string>> GroupNodeIds(
        IReadOnlyList<string> nodeIds,
        string workspaceRoot)
    {
        var groups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var nodeId in nodeIds.Where(id => !string.IsNullOrWhiteSpace(id)))
        {
            var filePart = IpyTestPath.FileFromNodeid(nodeId);
            var fullPath = PytestPathResolver.ResolvePath(filePart, workspaceRoot);
            if (!groups.TryGetValue(fullPath, out var list))
            {
                list = [];
                groups[fullPath] = list;
            }

            list.Add(nodeId);
        }

        return groups;
    }

    private async Task<DriverPayload> RunFileAsync(
        string testPath,
        IReadOnlyList<string> nodeIds,
        string workspaceRoot,
        CancellationToken cancellationToken)
    {
        var driverPath = PythonEmbedded.IpyTestDriverScriptPath;
        var driverDir = Path.GetDirectoryName(driverPath)
                        ?? throw new InvalidOperationException("IpyTestDriver path has no directory.");
        var requestPath = Path.Combine(driverDir, "request.json");
        var resultPath = Path.Combine(driverDir, "result.json");
        var prefix = IpyTestPath.ToNodeidPrefix(testPath, workspaceRoot);

        var requestBody = JsonSerializer.Serialize(new
        {
            test_path = testPath,
            workspace_root = workspaceRoot,
            nodeid_prefix = prefix,
            selected = nodeIds,
            result_path = resultPath,
        });
        File.WriteAllText(requestPath, requestBody);

        var previous = Environment.GetEnvironmentVariable(RequestEnvVar);
        Environment.SetEnvironmentVariable(RequestEnvVar, requestPath);
        try
        {
            var strategy = strategyFactory.Create(ExecutionMode.IronPython, driverPath, workspaceRoot);
            var exec = await strategy.ExecuteAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            return ReadPayload(resultPath, testPath, prefix, exec);
        }
        finally
        {
            Environment.SetEnvironmentVariable(RequestEnvVar, previous);
        }
    }

    private static DriverPayload ReadPayload(
        string resultPath,
        string testPath,
        string prefix,
        Models.ExecutionResult exec)
    {
        if (!File.Exists(resultPath))
        {
            var message = exec.Success
                ? "IronPython test driver produced no result file."
                : exec.Message;
            return new DriverPayload(
                "",
                [],
                [new PytestCollectionError(prefix, testPath, message, exec.Exception?.ToString() ?? string.Empty)]);
        }

        try
        {
            var json = File.ReadAllText(resultPath);
            var payload = JsonSerializer.Deserialize<DriverPayloadDto>(json, JsonOptions);
            if (payload is null)
            {
                return new DriverPayload(
                    "",
                    [],
                    [new PytestCollectionError(prefix, testPath, "Invalid IronPython test JSON.", json)]);
            }

            return new DriverPayload(
                payload.Engine ?? "",
                payload.Results ?? [],
                payload.CollectionErrors ?? []);
        }
        catch (Exception ex)
        {
            return new DriverPayload(
                "",
                [],
                [new PytestCollectionError(prefix, testPath, "Failed to read IronPython test JSON.", ex.ToString())]);
        }
    }

    private sealed record DriverPayload(
        string Engine,
        IReadOnlyList<PytestCaseResult> Results,
        IReadOnlyList<PytestCollectionError> CollectionErrors);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed class DriverPayloadDto
    {
        [System.Text.Json.Serialization.JsonPropertyName("engine")]
        public string? Engine { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("results")]
        public List<PytestCaseResult>? Results { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("collection_errors")]
        public List<PytestCollectionError>? CollectionErrors { get; set; }
    }
}
