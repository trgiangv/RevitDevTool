using System.Text.Json.Serialization;

namespace DevTools.Execution.External.Testing;

[UsedImplicitly]
public sealed record PytestRunRequest(
    [property: JsonPropertyName("workspace_root")] string WorkspaceRoot,
    [property: JsonPropertyName("test_root")] string TestRoot,
    [property: JsonPropertyName("nodeids")] IReadOnlyList<string> NodeIds,
    [property: JsonPropertyName("pytest_args")] IReadOnlyList<string> PytestArgs);

[UsedImplicitly]
public sealed record PytestRunResponse(
    [property: JsonPropertyName("exit_code")] int ExitCode,
    [property: JsonPropertyName("summary")] PytestSummary Summary,
    [property: JsonPropertyName("results")] IReadOnlyList<PytestCaseResult> Results,
    [property: JsonPropertyName("collection_errors")] IReadOnlyList<PytestCollectionError> CollectionErrors,
    [property: JsonPropertyName("rootdir")] string Rootdir,
    [property: JsonPropertyName("engine")] string Engine = "");

[UsedImplicitly]
public sealed record PytestSummary(
    [property: JsonPropertyName("passed")] int Passed,
    [property: JsonPropertyName("failed")] int Failed,
    [property: JsonPropertyName("skipped")] int Skipped,
    [property: JsonPropertyName("errors")] int Errors,
    [property: JsonPropertyName("xfailed")] int XFailed,
    [property: JsonPropertyName("xpassed")] int XPassed);

[UsedImplicitly]
public sealed record PytestCaseResult(
    [property: JsonPropertyName("nodeid")] string NodeId,
    [property: JsonPropertyName("outcome")] string Outcome,
    [property: JsonPropertyName("phase")] string Phase,
    [property: JsonPropertyName("duration_ms")] double DurationMs,
    [property: JsonPropertyName("stdout")] string Stdout,
    [property: JsonPropertyName("stderr")] string Stderr,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("traceback")] string Traceback);

[UsedImplicitly]
public sealed record PytestCollectionError(
    [property: JsonPropertyName("nodeid")] string NodeId,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("traceback")] string Traceback);

[UsedImplicitly]
internal sealed record IpyDriverFileRequest(
    [property: JsonPropertyName("test_path")] string TestPath,
    [property: JsonPropertyName("workspace_root")] string WorkspaceRoot,
    [property: JsonPropertyName("nodeid_prefix")] string NodeidPrefix,
    [property: JsonPropertyName("selected")] IReadOnlyList<string> Selected,
    [property: JsonPropertyName("result_path")] string ResultPath,
    [property: JsonPropertyName("maxfail")] int Maxfail);
