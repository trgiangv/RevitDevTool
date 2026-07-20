using System.IO;
using System.Text.Json;
using DevTools.Execution.Providers.Python;
using Python.Runtime;
// ReSharper disable ConditionalAccessQualifierIsNonNullableAccordingToAPIContract

namespace DevTools.Execution.External.Testing;

public class PytestExecutionService(PythonExecutor executor)
{
    private static readonly JsonSerializerOptions RequestOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly PytestSummary EmptySummary = new(0, 0, 0, 1, 0, 0);

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

        if (request is null)
        {
            error = "Pytest run request is required.";
            return false;
        }

        if (!ValidateRunRequest(request, out error))
            return false;

        request = NormalizeRunRequest(request);
        return true;
    }

    public static bool ValidateRunRequest(PytestRunRequest request, out string? error)
    {
        if (string.IsNullOrWhiteSpace(request.WorkspaceRoot) || !Path.IsPathRooted(request.WorkspaceRoot))
        {
            error = "workspace_root must be an absolute existing directory.";
            return false;
        }

        var workspaceRoot = Path.GetFullPath(request.WorkspaceRoot);
        if (!Directory.Exists(workspaceRoot))
        {
            error = "workspace_root must be an existing directory.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.TestRoot))
        {
            error = "test_root is required.";
            return false;
        }

        var testRoot = PytestPathResolver.ResolvePath(request.TestRoot, workspaceRoot);
        if (!IsWithinWorkspace(testRoot, workspaceRoot))
        {
            error = "test_root must be contained within workspace_root.";
            return false;
        }

        if (!(request.NodeIds?.Any(nodeId => !string.IsNullOrWhiteSpace(nodeId)) ?? false))
        {
            error = "At least one nodeid is required.";
            return false;
        }

        error = null;
        return true;
    }

    public static PytestRunRequest NormalizeRunRequest(PytestRunRequest request)
    {
        var workspaceRoot = Path.GetFullPath(request.WorkspaceRoot);
        var nodeIds = request.NodeIds?.Where(nodeId => !string.IsNullOrWhiteSpace(nodeId)).ToList() ?? [];
        var pytestArgs = request.PytestArgs?.Where(arg => !string.IsNullOrWhiteSpace(arg)).ToList() ?? [];
        return new PytestRunRequest(
            workspaceRoot,
            PytestPathResolver.ResolvePath(request.TestRoot, workspaceRoot),
            nodeIds,
            pytestArgs);
    }

    public virtual PytestRunResponse Run(PytestRunRequest request, Action<string>? progressCallback = null)
    {
        var runnerRequest = new PytestRunnerRequest(
            request.WorkspaceRoot,
            request.TestRoot,
            request.NodeIds,
            request.PytestArgs,
            false);

        return Execute<PytestRunResponse>(runnerRequest, request.TestRoot, progressCallback);
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

    private T Execute<T>(PytestRunnerRequest request, string anchorPath, Action<string>? progressCallback = null)
    {
        var rootFolder = ResolveRootFolder(request);
        var anchorFile = ResolveAnchorFile(anchorPath, rootFolder);

        return executor.Execute(
            anchorFile,
            rootFolder,
            scope =>
            {
                scope.Set(PythonInstances.PytestRequestJson, new PyString(JsonSerializer.Serialize(request)));

                if (progressCallback is not null)
                    scope.Set(PythonInstances.ProgressCallback, progressCallback.ToPython());

                scope.Exec(PythonEmbedded.PytestRunnerScript);

                var resultJson = scope.Get(PythonInstances.ResultJson).As<string>();
                var response = JsonSerializer.Deserialize<T>(resultJson, RequestOptions);
                return response ?? throw new InvalidOperationException("Pytest runner returned an empty response.");
            });
    }

    private static string ResolveRootFolder(PytestRunnerRequest request)
    {
        return Directory.Exists(request.TestRoot)
            ? request.TestRoot
            : Path.GetDirectoryName(request.TestRoot) ?? request.WorkspaceRoot;
    }

    /// <summary>
    /// Resolve anchor file for Python scope's <c>__file__</c> variable.
    /// Falls back to the root folder path itself when no concrete file exists,
    /// avoiding phantom file references.
    /// </summary>
    private static string ResolveAnchorFile(string anchorPath, string rootFolder)
    {
        if (File.Exists(anchorPath))
            return anchorPath;

        var initFile = Path.Combine(rootFolder, "__init__.py");
        return File.Exists(initFile) ? initFile : rootFolder;
    }

    private static bool IsWithinWorkspace(string path, string workspaceRoot)
    {
        var normalizedPath = Path.GetFullPath(path);
        var normalizedWorkspace = Path.TrimEndingDirectorySeparator(Path.GetFullPath(workspaceRoot));
        if (string.Equals(normalizedPath, normalizedWorkspace, StringComparison.OrdinalIgnoreCase))
            return true;

        var workspacePrefix = normalizedWorkspace + Path.DirectorySeparatorChar;
        return normalizedPath.StartsWith(workspacePrefix, StringComparison.OrdinalIgnoreCase);
    }
}
