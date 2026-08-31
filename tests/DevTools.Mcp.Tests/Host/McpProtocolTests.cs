using System.Text.Json.Nodes;
using DevTools.Mcp.Core.Protocol;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Tests.Host;

public sealed class McpProtocolTests
{
    [Fact]
    public void GetVersion_ReadsMetaField()
    {
        var parameters = new JsonObject
        {
            [McpSpecKeys.Meta.Key] = new JsonObject
            {
                [MetaKeys.ProtocolVersion] = McpSpecKeys.ProtocolVersions.Current,
            },
        };

        Assert.Equal(McpSpecKeys.ProtocolVersions.Current, McpProtocol.GetVersion(parameters));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2025-11-25")]
    public void IsCurrent_RejectsNonCurrentVersions(string? version)
    {
        Assert.False(McpProtocol.IsCurrent(version));
    }

    [Fact]
    public void IsCurrent_AcceptsCurrentVersion()
    {
        Assert.True(McpProtocol.IsCurrent(McpSpecKeys.ProtocolVersions.Current));
    }
}
