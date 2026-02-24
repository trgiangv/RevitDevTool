using RevitDevTool.Logger.Config;
using RevitDevTool.Logger.Enums;
using System.Diagnostics;
using System.Text.Json.Serialization;
namespace RevitDevTool.Settings.Config;

[Serializable]
public sealed class LogConfig : LogConfigCore
{
    [JsonPropertyName("useExternalFileOnly")]
    public bool UseExternalFileOnly { get; set; }

    [JsonPropertyName("includeWpfTrace")]
    public bool IncludeWpfTrace { get; set; }

    [JsonPropertyName("wpfTraceLevel")]
    public SourceLevels WpfTraceLevel { get; set; } = SourceLevels.Warning;

    [JsonPropertyName("revitEnrichers")]
    public RevitEnricher RevitEnrichers { get; set; } = RevitEnricher.RevitVersion | RevitEnricher.RevitDocumentTitle;
}
