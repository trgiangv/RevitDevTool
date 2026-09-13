using System.Text.Json.Nodes;
using DevTools.Mcp.Core.Protocol;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Core.Tests;

[TestClass]
public sealed class McpProtocolVersionTests
{
    [TestMethod]
    public void GetVersion_ReadsMetaProtocolVersion()
    {
        var parameters = new JsonObject
        {
            [McpSpecKeys.Meta.Key] = new JsonObject
            {
                [MetaKeys.ProtocolVersion] = "2026-07-28",
            },
        };

        Assert.AreEqual("2026-07-28", McpProtocol.GetVersion(parameters));
    }

    [TestMethod]
    public void GetVersion_MissingMeta_ReturnsNull()
    {
        Assert.IsNull(McpProtocol.GetVersion(new JsonObject()));
        Assert.IsNull(McpProtocol.GetVersion(null));
    }

    [TestMethod]
    [DataRow("2026-07-28", true)]
    [DataRow("2025-11-25", false)]
    [DataRow(null, false)]
    public void IsCurrent_MatchesExpectedVersion(string? version, bool expected)
    {
        Assert.AreEqual(expected, McpProtocol.IsCurrent(version));
    }
}
