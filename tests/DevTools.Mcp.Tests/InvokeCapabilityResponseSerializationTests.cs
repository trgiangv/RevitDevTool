using System.Text.Json;
using DevTools.Mcp.Server.Contracts;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Tests;

/// <summary>
/// P0-B characterization: bare <see cref="McpServerJsonContext"/> cannot
/// serialize <c>object?</c> holding SDK <see cref="ReadResourceResult"/>.
/// Production batch writes use <see cref="McpToolJson.Options"/> (passes).
/// Closing the union is ADR 0031 AOT follow-up, not this test.
/// </summary>
public sealed class InvokeCapabilityResponseSerializationTests
{
    [Fact]
    public void BatchReadResult_WithSdkReadResourceResult_ThrowsOnSerialize()
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

        var response = new InvokeCapabilityResponse(
            true,
            true,
            Results:
            [
                new ResourceReadResult(0, true, resource),
            ]);

        var ex = Assert.Throws<NotSupportedException>(() =>
            JsonSerializer.Serialize(response, McpServerJsonContext.Default.InvokeCapabilityResponse));

        Assert.Contains("ReadResourceResult", ex.Message, StringComparison.Ordinal);
    }
}
