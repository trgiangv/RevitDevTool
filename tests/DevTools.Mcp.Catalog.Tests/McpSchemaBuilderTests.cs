using System.Text.Json.Nodes;
using DevTools.Mcp.Catalog.Discovery;

namespace DevTools.Mcp.Catalog.Tests;

public sealed class McpSchemaBuilderTests
{
    [Fact]
    public void BuildSchema_MapsCollectionsEnumsAndObjectsWithoutFallingBackToString()
    {
        var schema = McpSchemaBuilder.BuildSchema(typeof(Arguments));

        Assert.Equal("object", schema["type"]?.GetValue<string>());
        Assert.Equal("array", schema["properties"]?["ids"]?["type"]?.GetValue<string>());
        Assert.Equal("integer", schema["properties"]?["ids"]?["items"]?["type"]?.GetValue<string>());
        Assert.Equal("string", schema["properties"]?["mode"]?["type"]?.GetValue<string>());
        Assert.Contains("Fast", schema["properties"]?["mode"]?["enum"]?.AsArray().Select(x => x!.GetValue<string>()) ?? []);
        Assert.Equal("object", schema["properties"]?["options"]?["type"]?.GetValue<string>());
    }

    private sealed class Arguments
    {
        public List<long> Ids { get; init; } = [];
        public RunMode Mode { get; init; }
        public Options Options { get; init; } = new();
    }

    private sealed class Options
    {
        public bool IncludeHidden { get; init; }
    }

    private enum RunMode
    {
        Fast,
        Safe,
    }
}
