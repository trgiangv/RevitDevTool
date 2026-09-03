using System.Text.Json.Serialization;
using DevTools.Hosting;
using DevTools.Ipc;

namespace DevTools.Daemon.Control;

[UsedImplicitly]
public sealed record StatusResponse(
    [property: JsonPropertyName(IpcPropertyNames.IsRunning)] bool IsRunning,
    [property: JsonPropertyName(IpcPropertyNames.Version)] string Version);

[UsedImplicitly]
public sealed record AuthStateResponse(
    [property: JsonPropertyName("isAuthenticated")] bool IsAuthenticated,
    [property: JsonPropertyName("userId")] string? UserId,
    [property: JsonPropertyName("email")] string? Email,
    [property: JsonPropertyName("displayName")] string? DisplayName,
    [property: JsonPropertyName("avatarUrl")] string? AvatarUrl);

[UsedImplicitly]
public sealed record OperationResponse(
    [property: JsonPropertyName(IpcPropertyNames.Success)] bool Success,
    [property: JsonPropertyName(IpcPropertyNames.Error)] string? Error = null);

[UsedImplicitly]
public sealed record ErrorResponse(
    [property: JsonPropertyName(IpcPropertyNames.Error)] string Error);

[UsedImplicitly]
public sealed record HostInfoEntry(
    [property: JsonPropertyName(IpcPropertyNames.HostApp)]
    [property: JsonConverter(typeof(JsonStringEnumConverter<HostApp>))]
    HostApp? HostApp,
    [property: JsonPropertyName(IpcPropertyNames.Version)] string? Version,
    [property: JsonPropertyName("pid")] int Pid,
    [property: JsonPropertyName(IpcPropertyNames.PipeName)] string PipeName);
