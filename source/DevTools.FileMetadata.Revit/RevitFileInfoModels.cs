using System.Text.Json.Serialization;
using DevTools.FileMetadata.Core;

namespace DevTools.FileMetadata.Revit;

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
public sealed class RevitFileInfoSummaryResult : FileInfoResult
{
    [JsonPropertyName("basicInfo")]
    public required RevitBasicInfoSummary BasicInfo { get; init; }

    [JsonPropertyName("projectTitle")]
    public string? ProjectTitle { get; init; }

    [JsonPropertyName("projectName")]
    public string? ProjectName { get; init; }

    [JsonPropertyName("externalReferenceCount")]
    public int ExternalReferenceCount { get; init; }

    [JsonPropertyName("externalReferences")]
    public IReadOnlyList<ExternalReferenceSummary>? ExternalReferences { get; init; }

    [JsonPropertyName("worksetCount")]
    public int WorksetCount { get; init; }
}

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record RevitBasicInfoSummary
{
    [JsonPropertyName("fileVersion")]
    public int FileVersion { get; init; }

    [JsonPropertyName("revitVersion")]
    public string? RevitVersion { get; init; }

    [JsonPropertyName("isWorkshared")]
    public bool IsWorkshared { get; init; }

    [JsonPropertyName("worksharingType")]
    public string WorksharingType { get; init; } = "Not enabled";

    [JsonPropertyName("locale")]
    public string? Locale { get; init; }
}

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record ExternalReferenceSummary
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("path")]
    public string? Path { get; init; }
}
