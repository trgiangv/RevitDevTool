using System.Text.Json.Nodes;
using DevTools.Mcp.Discovery;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevTools.Mcp.Catalog.Tests;

[TestClass]
public sealed class McpSchemaBuilderPreservationTests
{
    [TestMethod]
    public void BuildSchema_DeepNestingAndObjectType_ArePreserved()
    {
        var schema = McpSchemaBuilder.BuildSchema(typeof(Root));

        Assert.AreEqual("object", schema["type"]?.GetValue<string>());
        var level1 = schema["properties"]?["level1"]?.AsObject();
        Assert.IsNotNull(level1);
        Assert.AreEqual("object", level1!["type"]?.GetValue<string>());

        var level5 = level1!["properties"]?["level2"]?["properties"]?["level3"]?["properties"]?["level4"]?["properties"]?["level5"];
        Assert.IsNotNull(level5);
        Assert.AreEqual("object", level5!["type"]?.GetValue<string>());

        var untyped = McpSchemaBuilder.BuildSchema(typeof(object));
        Assert.AreEqual("object", untyped["type"]?.GetValue<string>());
    }

    [TestMethod]
    public void BuildSchema_PreservesDescriptionOnProperties()
    {
        var schema = McpSchemaBuilder.BuildSchema(typeof(DescribedArgs));
        Assert.AreEqual("Topic name.", schema["properties"]?["topic"]?["description"]?.GetValue<string>());
    }

    private sealed class Root
    {
        public Level1 Level1 { get; init; } = new();
    }

    private sealed class Level1
    {
        public Level2 Level2 { get; init; } = new();
    }

    private sealed class Level2
    {
        public Level3 Level3 { get; init; } = new();
    }

    private sealed class Level3
    {
        public Level4 Level4 { get; init; } = new();
    }

    private sealed class Level4
    {
        public Level5 Level5 { get; init; } = new();
    }

    private sealed class Level5
    {
        public string Value { get; init; } = string.Empty;
    }

    private sealed class DescribedArgs
    {
        [System.ComponentModel.Description("Topic name.")]
        public string Topic { get; init; } = string.Empty;
    }
}
