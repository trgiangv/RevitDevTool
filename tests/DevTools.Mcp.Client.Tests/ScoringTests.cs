using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Client.Tests;

[TestClass]
public sealed class ScoringTests
{
    [TestMethod]
    public void Score_FillerTokenDoesNotDropAOneTokenHit()
    {
        var hit = Item("read_file");
        var corpus = Scoring.Build([hit]);

        var score = Scoring.Score(["read", "zzz", "qqq"], hit, corpus);

        Assert.IsGreaterThan(0, score);
    }

    [TestMethod]
    public void Score_HighDocumentFrequencyLosesToLowDocumentFrequency()
    {
        var common = new[] { Item("get_info"), Item("get_status"), Item("get_list") };
        var rare = Item("mechanical_equipment");
        var corpus = Scoring.Build([.. common, rare]);

        var commonScore = Scoring.Score(["get", "equipment"], common[0], corpus);
        var rareScore = Scoring.Score(["get", "equipment"], rare, corpus);

        Assert.IsGreaterThan(commonScore, rareScore);
    }

    [TestMethod]
    public void Score_ParameterNameTokenScoresAboveZero()
    {
        var schema = JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new { category = new { type = "string" } },
        });
        var hit = Item("query_elements", "lists model elements", new Tool
        {
            Name = "query_elements",
            InputSchema = schema,
        });
        var corpus = Scoring.Build([hit]);

        var score = Scoring.Score(["category"], hit, corpus);

        Assert.IsGreaterThan(0, score);
    }

    [TestMethod]
    public void Score_InputSchemaDescriptionAndEnumAreSearchable()
    {
        var schema = JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                value = new
                {
                    type = "string",
                    description = "Fire resistance classification for each door",
                    @enum = new[] { "One hour", "Two hours" },
                },
            },
        });
        var hit = Item("inspect_model", "inspect model state", new Tool
        {
            Name = "inspect_model",
            InputSchema = schema,
        });
        var corpus = Scoring.Build([hit]);

        var score = Scoring.Score(["resistance", "hour"], hit, corpus);

        Assert.IsGreaterThan(0, score);
    }

    [TestMethod]
    public void Score_OutputSchemaDescriptionAndNameAreSearchable()
    {
        var outputSchema = JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                scheduleRows = new { type = "array", description = "Exported schedule row values" },
            },
        });
        var hit = Item("inspect_model", "inspect model state", new Tool
        {
            Name = "inspect_model",
            InputSchema = JsonSerializer.SerializeToElement(new { type = "object" }),
            OutputSchema = outputSchema,
        });
        var corpus = Scoring.Build([hit]);

        var score = Scoring.Score(["schedule", "rows", "exported"], hit, corpus);

        Assert.IsGreaterThan(0, score);
    }

    [TestMethod]
    public void Score_RequiredInputParameterGetsAdditionalSchemaWeight()
    {
        var property = new { type = "string", description = "A value" };
        var required = Item("inspect_model", "inspect model", new Tool
        {
            Name = "inspect_model",
            InputSchema = JsonSerializer.SerializeToElement(new
            {
                type = "object",
                properties = new { value = property },
                required = new[] { "value" },
            }),
        });
        var optional = Item("inspect_model", "inspect model", new Tool
        {
            Name = "inspect_model",
            InputSchema = JsonSerializer.SerializeToElement(new
            {
                type = "object",
                properties = new { value = property },
            }),
        });
        var corpus = Scoring.Build([required, optional]);

        var requiredScore = Scoring.Score(["value"], required, corpus);
        var optionalScore = Scoring.Score(["value"], optional, corpus);

        Assert.IsGreaterThan(optionalScore, requiredScore);
    }

    [TestMethod]
    public void Tokenizer_SplitsPunctuationAroundDomainTerms()
    {
        var tokens = Tokenizer.Tokenize("Walls, category-filter.");

        Assert.AreEqual("walls category filter", string.Join(' ', tokens));
    }

    [TestMethod]
    public void Score_RecursiveSchemaReferenceTerminates()
    {
        const string schemaJson = """
            {
              "type": "object",
              "$defs": {
                "Node": {
                  "description": "A node has a leaf identity.",
                  "properties": {
                    "children": { "type": "array", "items": { "$ref": "#/$defs/Node" } }
                  }
                }
              },
              "properties": { "root": { "$ref": "#/$defs/Node" } }
            }
            """;
        using var schemaDocument = JsonDocument.Parse(schemaJson);
        var hit = Item("inspect_tree", "Inspect nested data", new Tool
        {
            Name = "inspect_tree",
            InputSchema = schemaDocument.RootElement.Clone(),
        });
        var corpus = Scoring.Build([hit]);

        var score = Scoring.Score(["leaf", "identity"], hit, corpus);

        Assert.IsGreaterThan(0, score);
    }

    [TestMethod]
    public void Score_EmptyQueryIsZero()
    {
        var hit = Item("read_file");
        var corpus = Scoring.Build([hit]);

        Assert.AreEqual(0, Scoring.Score([], hit, corpus));
    }

    private static CatalogItem Item(string target, string? description = null, Tool? tool = null) => new(CatalogType.Tool, target, description, 1, null!, tool);
}