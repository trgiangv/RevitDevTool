using System.Text.Json;
using DevTools.Mcp.Catalog;

namespace DevTools.Mcp.Catalog.Tests;

[TestClass]
public sealed class JsonSchemaModelsTests
{
    [TestMethod]
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

        Assert.AreEqual("object", document.RootElement.GetProperty("type").GetString());
        Assert.AreEqual("string", document.RootElement.GetProperty("properties").GetProperty("name").GetProperty("type").GetString());
        Assert.AreEqual("name", document.RootElement.GetProperty("required")[0].GetString());
    }

    [TestMethod]
    public void TryParse_ReturnsNull_ForBlankOrInvalidJson()
    {
        Assert.IsNull(JsonSchemaObject.TryParse(null));
        Assert.IsNull(JsonSchemaObject.TryParse("   "));
        Assert.IsNull(JsonSchemaObject.TryParse("{not-json"));
    }

    [TestMethod]
    public void TryParse_DeserializesValidSchema()
    {
        var parsed = JsonSchemaObject.TryParse("""{"type":"object","properties":{"count":{"type":"integer"}}}""");

        Assert.IsNotNull(parsed);
        Assert.AreEqual("object", parsed!.Type);
        Assert.AreEqual("integer", parsed.Properties!["count"].Type);
    }
}
