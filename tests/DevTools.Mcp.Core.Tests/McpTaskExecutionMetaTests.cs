using System.Text.Json.Nodes;
using DevTools.Mcp.Core;
using ModelContextProtocol.Extensions.Tasks;

namespace DevTools.Mcp.Core.Tests;

[TestClass]
public sealed class McpTaskExecutionMetaTests
{
    [TestMethod]
    public void ParseMode_MissingMeta_ReturnsDefault()
    {
        Assert.AreEqual(McpTaskExecutionMode.Synchronous, McpTaskExecutionMeta.ParseMode(null));
        Assert.AreEqual(McpTaskExecutionMode.Optional, McpTaskExecutionMeta.ParseMode(null, McpTaskExecutionMode.Optional));
    }

    [TestMethod]
    public void ParseMode_ValidString_ReturnsMode()
    {
        var meta = new JsonObject
        {
            [McpTaskExecutionMeta.MetaKey] = McpTaskExecutionMeta.Mode.Required,
        };

        Assert.AreEqual(McpTaskExecutionMode.Required, McpTaskExecutionMeta.ParseMode(meta));
    }

    [TestMethod]
    [DataRow("optional", McpTaskExecutionMode.Optional)]
    [DataRow("REQUIRED", McpTaskExecutionMode.Required)]
    public void ParseMode_IsCaseInsensitive(string value, McpTaskExecutionMode expected)
    {
        var meta = new JsonObject { [McpTaskExecutionMeta.MetaKey] = value };
        Assert.AreEqual(expected, McpTaskExecutionMeta.ParseMode(meta));
    }

    [TestMethod]
    public void ParseMode_UnknownValue_ReturnsDefault()
    {
        var meta = new JsonObject { [McpTaskExecutionMeta.MetaKey] = "not-a-mode" };
        Assert.AreEqual(McpTaskExecutionMode.Synchronous, McpTaskExecutionMeta.ParseMode(meta));
    }
}
