using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Core.Tests;

[TestClass]
public sealed class McpProtocolModelsTests
{
    [TestMethod]
    public void SdkTool_RoundTrips_ThroughSdkJsonOptions()
    {
        var descriptor = new Tool
        {
            Name = "get_demo_status",
            Title = "Get Demo Status",
            Description = "Return demo status.",
            InputSchema = JsonSerializer.SerializeToElement(new { type = "object" }),
            Annotations = new ToolAnnotations { IdempotentHint = true, OpenWorldHint = false },
        };

        var json = JsonSerializer.Serialize(descriptor, McpJsonUtilities.DefaultOptions);
        var roundTrip = JsonSerializer.Deserialize<Tool>(json, McpJsonUtilities.DefaultOptions);

        Assert.IsNotNull(roundTrip);
        Assert.AreEqual(descriptor.Name, roundTrip.Name);
        Assert.AreEqual(descriptor.Title, roundTrip.Title);
        Assert.IsTrue(roundTrip.Annotations?.IdempotentHint);
    }
}
