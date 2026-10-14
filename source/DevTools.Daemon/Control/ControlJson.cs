using System.Text.Json.Serialization;
using DevTools.Daemon.Auth;
using DevTools.Daemon.Gateway;
using DevTools.Ipc;

namespace DevTools.Daemon.Control;

[JsonSourceGenerationOptions]
[JsonSerializable(typeof(TokenData))]
[JsonSerializable(typeof(ControlCommand))]
[JsonSerializable(typeof(GatewayRegisterMessage))]
[JsonSerializable(typeof(GatewayHeartbeatMessage))]
[JsonSerializable(typeof(List<string>))]
internal sealed partial class ControlJsonContext : JsonSerializerContext;

internal sealed record ControlCommand(
    [property: JsonPropertyName(IpcPropertyNames.Method)] string? Method);
