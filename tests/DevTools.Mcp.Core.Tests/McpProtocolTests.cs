using DevTools.Mcp.Core.Protocol;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Core.Tests;

[TestClass]
public sealed class McpProtocolTests
{
    [TestMethod]
    public void EnsureCurrentProtocolMeta_AddsCurrentVersionWhenMissing()
    {
        var parameters = new CallToolRequestParams { Name = "ping" };

        McpProtocol.EnsureCurrentProtocolMeta(parameters);

        Assert.IsNotNull(parameters.Meta);
        Assert.AreEqual(
            McpSpecKeys.ProtocolVersions.Current,
            parameters.Meta![MetaKeys.ProtocolVersion]!.GetValue<string>());
    }

    [TestMethod]
    public void EnsureCurrentProtocolMeta_PreservesExistingVersion()
    {
        var parameters = new CallToolRequestParams { Name = "ping" };
        parameters.Meta = new System.Text.Json.Nodes.JsonObject
        {
            [MetaKeys.ProtocolVersion] = "custom-version",
        };

        McpProtocol.EnsureCurrentProtocolMeta(parameters);

        Assert.AreEqual("custom-version", parameters.Meta[MetaKeys.ProtocolVersion]!.GetValue<string>());
    }
}
