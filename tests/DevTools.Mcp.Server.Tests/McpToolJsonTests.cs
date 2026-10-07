using System.Text.Json;
using DevTools.Daemon.Mcp.Contracts;

namespace DevTools.Mcp.Server.Tests;

[TestClass]
public sealed class McpToolJsonTests
{
    [TestMethod]
    public void Options_ProvideMetadataForDictionaryArguments()
    {
        Assert.IsNotNull(McpToolJson.Options.GetTypeInfo(typeof(Dictionary<string, JsonElement>)));
    }
}
