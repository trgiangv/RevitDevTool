using DevTools.Mcp.Core.Protocol;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Tests;

public sealed class McpProtocolTests
{
    [Fact]
    public void EnsureCurrentProtocolMeta_AddsCurrentVersionWhenMissing()
    {
        var parameters = new CallToolRequestParams { Name = "ping" };

        McpProtocol.EnsureCurrentProtocolMeta(parameters);

        Assert.NotNull(parameters.Meta);
        Assert.Equal(
            McpSpecKeys.ProtocolVersions.Current,
            parameters.Meta![MetaKeys.ProtocolVersion]!.GetValue<string>());
    }

    [Fact]
    public void EnsureCurrentProtocolMeta_PreservesExistingVersion()
    {
        var parameters = new CallToolRequestParams { Name = "ping" };
        parameters.Meta = new System.Text.Json.Nodes.JsonObject
        {
            [MetaKeys.ProtocolVersion] = "custom-version",
        };

        McpProtocol.EnsureCurrentProtocolMeta(parameters);

        Assert.Equal("custom-version", parameters.Meta[MetaKeys.ProtocolVersion]!.GetValue<string>());
    }
}
