using System.Text.Json;

namespace DevTools.Mcp.Server.Contracts;

public sealed record SearchCapabilitiesRequest(
    string? Query = null,
    int? HostInstanceId = null,
    string[]? Kinds = null,
    int? Limit = null,
    string? Detail = null);

public sealed record SearchCapabilitiesResponse(int Count, bool HasMore, IReadOnlyList<SearchCapabilityItem> Items);

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

public sealed record InvokeCapabilityRequest(
    string? CapabilityId = null,
    JsonElement? Arguments = null,
    IReadOnlyList<ResourceReadRequest>? Reads = null);

public sealed record ResourceReadRequest(string? CapabilityId, Dictionary<string, JsonElement>? Arguments = null);

public sealed record InvokeCapabilityResponse(
    bool Ok,
    bool ExecutionStarted,
    object? Result = null,
    DynamicInvocationError? Error = null,
    IReadOnlyList<ResourceReadResult>? Results = null);

public sealed record ResourceReadResult(int Index, bool Ok, object? Result = null, DynamicInvocationError? Error = null);

public sealed record DynamicInvocationError(string Type, string Message, bool Retryable = false, string? Reason = null, string? Retry = null);
