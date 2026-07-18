using System.Text.Json.Serialization;
using DevTools.Logging;

namespace DevTools.Daemon.Contracts;

[UsedImplicitly]
public sealed record StatusResponse(
    [property: JsonPropertyName(DaemonConstants.JsonProperties.IsRunning)] bool IsRunning,
    [property: JsonPropertyName("version")] string Version);

[UsedImplicitly]
public sealed record AuthStateResponse(
    [property: JsonPropertyName("isAuthenticated")] bool IsAuthenticated,
    [property: JsonPropertyName("userId")] string? UserId,
    [property: JsonPropertyName("email")] string? Email,
    [property: JsonPropertyName("displayName")] string? DisplayName,
    [property: JsonPropertyName("avatarUrl")] string? AvatarUrl);

[UsedImplicitly]
public sealed record OperationResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("error")] string? Error = null);

[UsedImplicitly]
public sealed record ErrorResponse(
    [property: JsonPropertyName("error")] string Error);

[UsedImplicitly]
public sealed record HostInfoEntry(
    [property: JsonPropertyName("hostApp")]
    [property: JsonConverter(typeof(JsonStringEnumConverter<HostApp>))]
    HostApp? HostApp,
    [property: JsonPropertyName("version")] string? Version,
    [property: JsonPropertyName("pid")] int Pid,
    [property: JsonPropertyName("pipeName")] string PipeName);
