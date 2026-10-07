using System.Text.Json;
using DevTools.Daemon.Mcp.Processes;
using DevTools.Daemon.Mcp.Search;
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
        var corpus = Scoring.Build([..common, rare]);

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
    public void Score_EmptyQueryIsZero()
    {
        var hit = Item("read_file");
        var corpus = Scoring.Build([hit]);

        Assert.AreEqual(0, Scoring.Score([], hit, corpus));
    }

    private static CatalogItem Item(string target, string? description = null, Tool? tool = null) =>
        new(CatalogType.Tool, target, description, 1, null!, tool);
}
