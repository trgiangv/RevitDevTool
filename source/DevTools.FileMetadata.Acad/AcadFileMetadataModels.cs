using System.Text.Json.Serialization;
using DevTools.FileMetadata.Core;

namespace DevTools.FileMetadata.Acad;

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

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed class DwgFileInfoSummaryResult : FileInfoResult
{
    [JsonPropertyName("acadVersion")]
    public string? AcadVersion { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("author")]
    public string? Author { get; init; }

    [JsonPropertyName("layerCount")]
    public int LayerCount { get; init; }

    [JsonPropertyName("blockCount")]
    public int BlockCount { get; init; }
}

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed class DwgLayerInfo
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("isOn")]
    public bool IsOn { get; init; }
}
