using System.Text.Json.Nodes;
using DevTools.Mcp.Catalog.Discovery;

namespace DevTools.Mcp.Catalog.Tests;

[TestClass]
public sealed class McpSchemaBuilderTests
{
    [TestMethod]
    public void BuildSchema_MapsCollectionsEnumsAndObjectsWithoutFallingBackToString()
    {
        var schema = McpSchemaBuilder.BuildSchema(typeof(Arguments));

        Assert.AreEqual("object", schema["type"]?.GetValue<string>());
        Assert.AreEqual("array", schema["properties"]?["ids"]?["type"]?.GetValue<string>());
        Assert.AreEqual("integer", schema["properties"]?["ids"]?["items"]?["type"]?.GetValue<string>());
        Assert.AreEqual("string", schema["properties"]?["mode"]?["type"]?.GetValue<string>());
        Assert.Contains("Fast", schema["properties"]?["mode"]?["enum"]?.AsArray().Select(x => x!.GetValue<string>()) ?? []);
        Assert.AreEqual("object", schema["properties"]?["options"]?["type"]?.GetValue<string>());
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
