using System.Text.Json.Serialization;
using ModelContextProtocol.Protocol;

namespace DevTools.Execution.External.Mcp.BuiltIn;

public static class PytestMcpErrorCodes
{
    public const string InvalidInput = "pytest_invalid_input";
    public const string DependencyPreparationFailed = "pytest_dependency_preparation_failed";
    public const string HostContextUnavailable = "pytest_host_context_unavailable";
    public const string RunnerFailed = "pytest_runner_failed";
    public const string SerializationFailed = "pytest_serialization_failed";
    public const string HostShuttingDown = "pytest_host_shutting_down";
}

public sealed record PytestCaseEvent(
    [property: JsonPropertyName("progressToken")] ProgressToken ProgressToken,
    [property: JsonPropertyName("sequence")] int Sequence,
    [property: JsonPropertyName("case")] PytestCaseResult Case);
