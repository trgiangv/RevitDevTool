using System.Text.Json.Serialization;

namespace DevTools.Ipc;

public sealed class InstanceInfo
{
    [JsonPropertyName("hostApp")]
    public string? HostApp { get; init; }

    [JsonPropertyName("processId")]
    public int ProcessId { get; init; }

    [JsonPropertyName("versionNumber")]
    public string VersionNumber { get; init; } = string.Empty;
}
