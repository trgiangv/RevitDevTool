using DevTools.Ipc;
using DevTools.Utilities;
using System.Net.WebSockets;
using System.Text.Json.Serialization;

namespace DevTools.Daemon.Tunnel;

[UsedImplicitly]
public sealed record GatewayRegisterMessage(
    [property: JsonPropertyName(IpcPropertyNames.Type)] string Type,
    [property: JsonPropertyName("machine_id")] string MachineId,
    [property: JsonPropertyName("machine_name")] string MachineName,
    [property: JsonPropertyName("host_apps")] IReadOnlyList<string> HostApps,
    [property: JsonPropertyName("sender")] ProtocolSender Sender);

[UsedImplicitly]
public sealed record GatewayHeartbeatMessage(
    [property: JsonPropertyName(IpcPropertyNames.Type)] string Type,
    [property: JsonPropertyName("host_apps")] IReadOnlyList<string> HostApps);

internal static class TunnelProtocol
{
    public static ProtocolSender Sender { get; } = new(AppVersion.Current, new ProtocolRange(1, 1));

    public const WebSocketCloseStatus IncompatibleCloseStatus = (WebSocketCloseStatus)4001; // RFC 6455 private range; not in the enum
}
