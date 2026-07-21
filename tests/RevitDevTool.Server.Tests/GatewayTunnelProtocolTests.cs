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
    public void RegisteredEnvelope_RequiresConnectionGenerationAndGatewayVersion()
    {
        using var missingGeneration = JsonDocument.Parse("""{"v":2,"type":"registered","gateway_version":"2.0.0"}""");
        Assert.False(GatewayTunnelEnvelope.TryParse(missingGeneration.RootElement, out _, out var error));
        Assert.Equal("invalid_tunnel_frame", error);

        using var missingGatewayVersion = JsonDocument.Parse("""{"v":2,"type":"registered","connection_generation":1}""");
        Assert.False(GatewayTunnelEnvelope.TryParse(missingGatewayVersion.RootElement, out _, out error));
        Assert.Equal("invalid_tunnel_frame", error);

        using var valid = JsonDocument.Parse("""{"v":2,"type":"registered","connection_generation":1,"gateway_version":"2.0.0"}""");
        Assert.True(GatewayTunnelEnvelope.TryParse(valid.RootElement, out var envelope, out error));
        Assert.Equal(GatewayTunnelEnvelope.Registered, envelope!.Type);
        Assert.Equal(1, envelope.ConnectionGeneration);
        Assert.Equal("2.0.0", envelope.GatewayVersion);
        Assert.Null(error);
    }

    [Fact]
    public void RegisterEnvelope_RequiresDaemonVersion()
    {
        using var missing = JsonDocument.Parse("""{"v":2,"type":"register","machine_id":"m1","machine_name":"pc","host_apps":["Revit"]}""");
        Assert.False(GatewayTunnelEnvelope.TryParse(missing.RootElement, out _, out var error));
        Assert.Equal("invalid_tunnel_frame", error);

        using var valid = JsonDocument.Parse("""{"v":2,"type":"register","machine_id":"m1","machine_name":"pc","host_apps":["Revit"],"daemon_version":"4.0.0"}""");
        Assert.True(GatewayTunnelEnvelope.TryParse(valid.RootElement, out var envelope, out error));
        Assert.Equal(GatewayTunnelEnvelope.Register, envelope!.Type);
        Assert.Equal("4.0.0", envelope.DaemonVersion);
        Assert.Null(error);
    }

    [Fact]
    public void RejectsNonStringHostAppsWithoutThrowing()
    {
        using var document = JsonDocument.Parse("""{"v":2,"type":"heartbeat","host_apps":["Revit_2027_1",42]}""");

        Assert.False(GatewayTunnelEnvelope.TryParse(document.RootElement, out _, out var error));
        Assert.Equal("invalid_tunnel_frame", error);
    }
}
