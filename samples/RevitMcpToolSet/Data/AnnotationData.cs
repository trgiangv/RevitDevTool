using System.ComponentModel;
using System.Text.Json.Serialization;

namespace RevitMcpToolSet.Data;

public class TagPlacement
{
    [Description("View ID to tag in.")]
    [JsonPropertyName("viewId")]
    public long ViewId { get; set; }

    [Description("Element IDs to tag.")]
    [JsonPropertyName("elementIds")]
    public long[] ElementIds { get; set; } = [];

    [JsonIgnore]
    public long[] ElementsIds
    {
        get => ElementIds;
        set => ElementIds = value;
    }
}
