using System.Text.Json.Serialization;

namespace DevTools.Logging.Options;

[Serializable]
public class HttpLoggingOptions
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("endpoint")]
    public string Endpoint { get; set; } = string.Empty;

    [JsonPropertyName("batchSize")]
    public int BatchSize { get; set; } = 100;

    [JsonPropertyName("format")]
    public SaveFormat Format { get; set; } = SaveFormat.Json;
}
