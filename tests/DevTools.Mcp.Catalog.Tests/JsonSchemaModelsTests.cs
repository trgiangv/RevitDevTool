using System.Text.Json;
using DevTools.Mcp.Catalog;

namespace DevTools.Mcp.Catalog.Tests;

public sealed class JsonSchemaModelsTests
{
    [Fact]
    public void ToElement_SerializesObjectSchema()
    {
        var schema = new JsonSchemaObject
        {
            Properties = new Dictionary<string, JsonSchemaProperty>
            {
                ["name"] = new() { Type = "string", Title = "Name" },
            },
            Required = ["name"],
        };

        using var document = JsonDocument.Parse(schema.ToElement().GetRawText());

        Assert.Equal("object", document.RootElement.GetProperty("type").GetString());
        Assert.Equal("string", document.RootElement.GetProperty("properties").GetProperty("name").GetProperty("type").GetString());
        Assert.Equal("name", document.RootElement.GetProperty("required")[0].GetString());
    }

    [Fact]
    public void TryParse_ReturnsNull_ForBlankOrInvalidJson()
    {
        Assert.Null(JsonSchemaObject.TryParse(null));
        Assert.Null(JsonSchemaObject.TryParse("   "));
        Assert.Null(JsonSchemaObject.TryParse("{not-json"));
    }

    [Fact]
    public void TryParse_DeserializesValidSchema()
    {
        var parsed = JsonSchemaObject.TryParse("""{"type":"object","properties":{"count":{"type":"integer"}}}""");

        Assert.NotNull(parsed);
        Assert.Equal("object", parsed!.Type);
        Assert.Equal("integer", parsed.Properties!["count"].Type);
    }
}
