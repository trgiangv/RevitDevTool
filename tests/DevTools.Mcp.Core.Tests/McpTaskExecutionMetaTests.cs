using System.Text.Json.Nodes;
using DevTools.Mcp.Core;
using ModelContextProtocol.Extensions.Tasks;

namespace DevTools.Mcp.Core.Tests;

public sealed class McpTaskExecutionMetaTests
{
    [Fact]
    public void ParseMode_MissingMeta_ReturnsDefault()
    {
        Assert.Equal(McpTaskExecutionMode.Synchronous, McpTaskExecutionMeta.ParseMode(null));
        Assert.Equal(McpTaskExecutionMode.Optional, McpTaskExecutionMeta.ParseMode(null, McpTaskExecutionMode.Optional));
    }

    [Fact]
    public void ParseMode_ValidString_ReturnsMode()
    {
        var meta = new JsonObject
        {
            [McpTaskExecutionMeta.MetaKey] = McpTaskExecutionMeta.Mode.Required,
        };

        Assert.Equal(McpTaskExecutionMode.Required, McpTaskExecutionMeta.ParseMode(meta));
    }

    [Theory]
    [InlineData("optional", McpTaskExecutionMode.Optional)]
    [InlineData("REQUIRED", McpTaskExecutionMode.Required)]
    public void ParseMode_IsCaseInsensitive(string value, McpTaskExecutionMode expected)
    {
        var meta = new JsonObject { [McpTaskExecutionMeta.MetaKey] = value };
        Assert.Equal(expected, McpTaskExecutionMeta.ParseMode(meta));
    }

    [Fact]
    public void ParseMode_UnknownValue_ReturnsDefault()
    {
        var meta = new JsonObject { [McpTaskExecutionMeta.MetaKey] = "not-a-mode" };
        Assert.Equal(McpTaskExecutionMode.Synchronous, McpTaskExecutionMeta.ParseMode(meta));
    }
}
