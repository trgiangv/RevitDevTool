using System.Text.Json.Serialization;

namespace DevTools.Daemon.Contracts;

[UsedImplicitly]
public sealed record GatewayRegisterMessage(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("machine_id")] string MachineId,
    [property: JsonPropertyName("machine_name")] string MachineName,
    [property: JsonPropertyName("host_apps")] IReadOnlyList<string> HostApps);

[UsedImplicitly]
public sealed record GatewayHeartbeatMessage(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("host_apps")] IReadOnlyList<string> HostApps);
