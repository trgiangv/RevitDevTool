using System.Text.Json.Serialization;
using DevTools.Ipc;

namespace DevTools.Daemon.Gateway;

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
