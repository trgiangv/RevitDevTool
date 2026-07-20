using System.Text.Json;
using DevTools.Daemon.Hosting;

namespace RevitDevTool.Server.Tests;

public sealed class GatewayTunnelProtocolTests
{
    [Fact]
    public void ParsesOpaqueV2McpMessage()
    {
        using var document = JsonDocument.Parse("""{"v":2,"type":"mcp.message","session_id":"gw_a","message":{"jsonrpc":"2.0","id":1,"method":"initialize"}}""");

        Assert.True(GatewayTunnelEnvelope.TryParse(document.RootElement, out var envelope, out var error));
        Assert.Equal(GatewayTunnelEnvelope.McpMessage, envelope!.Type);
        Assert.Equal("gw_a", envelope.SessionId);
        Assert.Equal(1, envelope.Message!.Value.GetProperty("id").GetInt32());
        Assert.Null(error);
    }

    [Fact]
    public void RejectsV1BeforeSessionDispatch()
    {
        using var document = JsonDocument.Parse("""{"v":1,"type":"mcp.message","session_id":"gw_a","message":{}}""");

        Assert.False(GatewayTunnelEnvelope.TryParse(document.RootElement, out _, out var error));
        Assert.Equal("unsupported_tunnel_protocol", error);
    }

    [Fact]
    public void RegisteredEnvelope_RequiresConnectionGeneration()
    {
        using var document = JsonDocument.Parse("""{"v":2,"type":"registered"}""");

        Assert.False(GatewayTunnelEnvelope.TryParse(document.RootElement, out _, out var error));
        Assert.Equal("invalid_tunnel_frame", error);
    }
}
