using System.Text.Json.Serialization;

namespace DevTools.Ipc;

public sealed class InstanceInfo
{
    [JsonPropertyName(IpcPropertyNames.HostApp)]
    public string? HostApp { get; init; }

    [JsonPropertyName(IpcPropertyNames.ProcessId)]
    public int ProcessId { get; init; }

    [JsonPropertyName(IpcPropertyNames.VersionNumber)]
    public string VersionNumber { get; init; } = string.Empty;
}
