using System.Text.Json.Nodes;
using DevTools.Mcp.Core.Protocol;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Core.Tests;

public sealed class McpProtocolVersionTests
{
    [Fact]
    public void GetVersion_ReadsMetaProtocolVersion()
    {
        var parameters = new JsonObject
        {
            [McpSpecKeys.Meta.Key] = new JsonObject
            {
                [MetaKeys.ProtocolVersion] = "2026-07-28",
            },
        };

        Assert.Equal("2026-07-28", McpProtocol.GetVersion(parameters));
    }

    [Fact]
    public void GetVersion_MissingMeta_ReturnsNull()
    {
        Assert.Null(McpProtocol.GetVersion(new JsonObject()));
        Assert.Null(McpProtocol.GetVersion(null));
    }

    [Theory]
    [InlineData("2026-07-28", true)]
    [InlineData("2025-11-25", false)]
    [InlineData(null, false)]
    public void IsCurrent_MatchesExpectedVersion(string? version, bool expected)
    {
        Assert.Equal(expected, McpProtocol.IsCurrent(version));
    }
}
