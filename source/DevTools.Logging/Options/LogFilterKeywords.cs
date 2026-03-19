using System.Text.Json.Serialization;

namespace DevTools.Logging.Options;

[Serializable]
public class LogFilterKeywords
{
    [JsonPropertyName("information")]
    public string Information { get; set; } = "info,success,completed";

    [JsonPropertyName("warning")]
    public string Warning { get; set; } = "warning,warn,caution";

    [JsonPropertyName("error")]
    public string Error { get; set; } = "error,failed,exception";

    [JsonPropertyName("critical")]
    public string Critical { get; set; } = "fatal,critical,crash";
}
