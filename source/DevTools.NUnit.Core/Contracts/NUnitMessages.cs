using System.Text.Json.Serialization;

namespace DevTools.NUnit.Core.Contracts;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record NUnitHelloRequest(
    [property: JsonPropertyName("protocol_version")] int ProtocolVersion);

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record NUnitHelloResponse(
    [property: JsonPropertyName("protocol_version")] int ProtocolVersion,
    [property: JsonPropertyName("host")] string Host,
    [property: JsonPropertyName("host_version")] string HostVersion,
    [property: JsonPropertyName("process_id")] int ProcessId,
    [property: JsonPropertyName("is_busy")] bool IsBusy);

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record NUnitDiscoverRequest(
    [property: JsonPropertyName("assembly_path")] string AssemblyPath,
    [property: JsonPropertyName("filter")] string? Filter);

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record NUnitDiscoveredTest(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("full_name")] string FullName);

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record NUnitDiscoverResponse(
    [property: JsonPropertyName("cases")] IReadOnlyList<NUnitDiscoveredTest> Cases);

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record NUnitRunRequest(
    [property: JsonPropertyName("run_id")] Guid RunId,
    [property: JsonPropertyName("assembly_path")] string AssemblyPath,
    [property: JsonPropertyName("filter")] string? Filter,
    [property: JsonPropertyName("wait_for_debugger")] bool WaitForDebugger);

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record NUnitRunSummary(
    [property: JsonPropertyName("passed")] int Passed,
    [property: JsonPropertyName("failed")] int Failed,
    [property: JsonPropertyName("skipped")] int Skipped,
    [property: JsonPropertyName("inconclusive")] int Inconclusive,
    [property: JsonPropertyName("errors")] int Errors,
    [property: JsonPropertyName("cancelled")] int Cancelled);

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record NUnitCaseResult(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("outcome")] string Outcome,
    [property: JsonPropertyName("duration_ms")] double DurationMilliseconds,
    [property: JsonPropertyName(IpcPropertyNames.Message)] string? Message,
    [property: JsonPropertyName("stack_trace")] string? StackTrace,
    [property: JsonPropertyName("output")] string? Output);

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record NUnitRunResponse(
    [property: JsonPropertyName("run_id")] Guid RunId,
    [property: JsonPropertyName("summary")] NUnitRunSummary Summary,
    [property: JsonPropertyName("cases")] IReadOnlyList<NUnitCaseResult> Cases);

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record NUnitProgressEvent(
    [property: JsonPropertyName("run_id")] Guid RunId,
    [property: JsonPropertyName("case")] NUnitCaseResult Case);

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record NUnitCancelRequest(
    [property: JsonPropertyName("run_id")] Guid RunId);
