using System.Text.Json.Serialization;

namespace DevTools.Daemon.Contracts;

// Gateway wire names are stable; host_apps is populated from connected session metadata, not pipe names.
[UsedImplicitly]
public sealed record GatewayRegisterMessage(
    [property: JsonPropertyName(IpcPropertyNames.Type)] string Type,
    [property: JsonPropertyName("machine_id")] string MachineId,
    [property: JsonPropertyName("machine_name")] string MachineName,
    [property: JsonPropertyName("host_apps")] IReadOnlyList<string> HostApps);

[UsedImplicitly]
public sealed record GatewayHeartbeatMessage(
    [property: JsonPropertyName(IpcPropertyNames.Type)] string Type,
    [property: JsonPropertyName("host_apps")] IReadOnlyList<string> HostApps);
