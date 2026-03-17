using System.ComponentModel;
namespace RevitMcpToolSet.Data;

public class TagPlacement
{
    [Description("View ID to tag in")] public long ViewId { get; set; }
    [Description("Element IDs to tag")] public long[] ElementsIds { get; set; } = [];
}
