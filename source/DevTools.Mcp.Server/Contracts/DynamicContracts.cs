using System.Text.Json;

namespace DevTools.Mcp.Server.Contracts;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record SearchCapabilitiesRequest(
    string? Query = null,
    int? HostInstanceId = null,
    string[]? Kinds = null,
    int? Limit = null,
    string? Detail = null);

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record SearchCapabilitiesResponse(int Count, bool HasMore, IReadOnlyList<SearchCapabilityItem> Items);

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record SearchCapabilityItem(
    string CapabilityId,
    string Kind,
    string Target,
    string? Description,
    string MachineId,
    int HostInstanceId,
    string? HostApp,
    string? VersionNumber,
    string[]? RequiredArgs,
    string[]? ArgsHint,
    JsonElement? InputSchema,
    string? MimeType);

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record InvokeCapabilityRequest(
    string? CapabilityId = null,
    JsonElement? Arguments = null,
    IReadOnlyList<ResourceReadRequest>? Reads = null);

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record ResourceReadRequest(string? CapabilityId, Dictionary<string, JsonElement>? Arguments = null);

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record InvokeCapabilityResponse(
    bool Ok,
    bool ExecutionStarted,
    object? Result = null,
    DynamicInvocationError? Error = null,
    IReadOnlyList<ResourceReadResult>? Results = null);

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record ResourceReadResult(int Index, bool Ok, object? Result = null, DynamicInvocationError? Error = null);

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record DynamicInvocationError(string Type, string Message, bool Retryable = false, string? Reason = null, string? Retry = null);
