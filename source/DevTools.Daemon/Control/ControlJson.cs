using System.Text.Json.Serialization;
using DevTools.Daemon.Auth;
using DevTools.Daemon.Gateway;

namespace DevTools.Daemon.Control;

[JsonSourceGenerationOptions]
[JsonSerializable(typeof(TokenData))]
[JsonSerializable(typeof(StatusResponse))]
[JsonSerializable(typeof(AuthStateResponse))]
[JsonSerializable(typeof(OperationResponse))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(HostInfoEntry))]
[JsonSerializable(typeof(HostInfoEntry[]))]
[JsonSerializable(typeof(GatewayRegisterMessage))]
[JsonSerializable(typeof(GatewayHeartbeatMessage))]
[JsonSerializable(typeof(List<string>))]
internal sealed partial class ControlJsonContext : JsonSerializerContext;
