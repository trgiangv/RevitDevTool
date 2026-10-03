using DevTools.Mcp.Discovery;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Catalog.Tests;

[TestClass]
public sealed class ResultBridgeTests
{
    [TestMethod]
    public void ToHostCallToolResult_SerializesForeignCallToolResultOnce()
    {
        var foreign = new CallToolResult
        {
            Content = [new TextContentBlock { Text = "bridged" }],
        };

        var host = ResultBridge.ToHostCallToolResult(foreign, outputSchema: null);

        Assert.AreEqual(typeof(CallToolResult), host.GetType());
        Assert.AreEqual("bridged", Assert.IsInstanceOfType<TextContentBlock>(host.Content.Single()).Text);
    }
}
