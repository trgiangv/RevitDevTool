using System.Text.Json;
using DevTools.Mcp.Server.Hosting;
using DevTools.Mcp.Core.Utils;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Tests;

public class McpLogPayloadTests
{
    [Fact]
    public void SerializeCallToolResult_ScrubsTextWrappedBlobResource()
    {
        var blobBytes = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        var resource = new ReadResourceResult
        {
            Contents =
            [
                BlobResourceContents.FromBytes(blobBytes, "image://test", "image/png")
            ]
        };

        var wrapped = ToolHelpers.Serialize(resource);
        var callResult = ToolHelpers.Result(wrapped);
        var logJson = McpLogPayload.SerializeCallToolResult(callResult);

        Assert.Contains("\"type\":\"blob\"", logJson, StringComparison.Ordinal);
        Assert.Contains("\"length\":4", logJson, StringComparison.Ordinal);
        Assert.DoesNotContain("3q2+7w==", logJson, StringComparison.Ordinal);
    }
}
