using System.Text.Json.Serialization;
using DevTools.Daemon.Mcp.AcadFileInfo;
using DevTools.Daemon.Mcp.RevitFileInfo;
using DevTools.Logging;

namespace DevTools.Daemon.Contracts;

/// <summary>
/// Base contract for file info results. Common metadata shared across all host types.
/// Each host subclass adds strongly-typed properties specific to that file format.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public abstract class FileInfoResult
{
    [JsonPropertyName("hostApp")]
    [JsonConverter(typeof(JsonStringEnumConverter<HostApp>))]
    public required HostApp HostApp { get; init; }

    [JsonPropertyName("filePath")]
    public required string FilePath { get; init; }

    [JsonPropertyName("fileName")]
    public required string FileName { get; init; }
}

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed class RevitFileInfoResult : FileInfoResult
{
    [JsonPropertyName("basicInfo")]
    public required BasicFileInfo BasicInfo { get; init; }

    [JsonPropertyName("transmissionData")]
    public TransmissionData? TransmissionData { get; init; }

    [JsonPropertyName("projectInformation")]
    public ProjectInformation? ProjectInformation { get; init; }

    [JsonPropertyName("worksets")]
    public IReadOnlyList<WorksetInfo>? Worksets { get; init; }

    [JsonPropertyName("partitionSummary")]
    public PartitionSummary? PartitionSummary { get; init; }

    [JsonPropertyName("browserOrganization")]
    public IReadOnlyList<string>? BrowserOrganization { get; init; }
}

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed class DwgFileInfoResult : FileInfoResult
{
    [JsonPropertyName("acadVersion")]
    public string? AcadVersion { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("subject")]
    public string? Subject { get; init; }

    [JsonPropertyName("author")]
    public string? Author { get; init; }

    [JsonPropertyName("keywords")]
    public string? Keywords { get; init; }

    [JsonPropertyName("comments")]
    public string? Comments { get; init; }

    [JsonPropertyName("lastSavedBy")]
    public string? LastSavedBy { get; init; }

    [JsonPropertyName("layerCount")]
    public int LayerCount { get; init; }

    [JsonPropertyName("blockCount")]
    public int BlockCount { get; init; }

    [JsonPropertyName("layers")]
    public IReadOnlyList<DwgLayerInfo>? Layers { get; init; }
}
