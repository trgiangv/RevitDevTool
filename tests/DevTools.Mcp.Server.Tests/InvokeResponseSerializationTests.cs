using System.Text.Json;
using DevTools.Daemon.Mcp.Contracts;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Server.Tests;

[TestClass]
public sealed class InvokeResponseSerializationTests
{
    [TestMethod]
    public void BatchReadResult_SerializesSdkReadResourceResult()
    {
        var resource = new ReadResourceResult
        {
            Contents =
            [
                new TextResourceContents
                {
                    Uri = "sample://status",
                    MimeType = "text/plain",
                    Text = "ok",
                },
            ],
        };

        var response = new InvokeResponse(
            true,
            true,
            Results:
            [
                new ResourceReadResult(0, true, resource),
            ]);

        var json = JsonSerializer.Serialize(response, McpToolJson.Options);

        Assert.Contains("sample://status", json, StringComparison.Ordinal);
        Assert.Contains("\"text\":\"ok\"", json, StringComparison.Ordinal);
    }
}
