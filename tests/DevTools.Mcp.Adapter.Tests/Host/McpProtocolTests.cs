using System.Text.Json.Nodes;
using DevTools.Mcp.Core.Protocol;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Adapter.Tests.Host;

[TestClass]
public sealed class McpProtocolTests
{
    [TestMethod]
    public void GetVersion_ReadsMetaField()
    {
        var parameters = new JsonObject
        {
            [McpSpecKeys.Meta.Key] = new JsonObject
            {
                [MetaKeys.ProtocolVersion] = McpSpecKeys.ProtocolVersions.Current,
            },
        };

        Assert.AreEqual(McpSpecKeys.ProtocolVersions.Current, McpProtocol.GetVersion(parameters));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("2025-11-25")]
    public void IsCurrent_RejectsNonCurrentVersions(string? version)
    {
        Assert.IsFalse(McpProtocol.IsCurrent(version));
    }

    [TestMethod]
    public void IsCurrent_AcceptsCurrentVersion()
    {
        Assert.IsTrue(McpProtocol.IsCurrent(McpSpecKeys.ProtocolVersions.Current));
    }
}
