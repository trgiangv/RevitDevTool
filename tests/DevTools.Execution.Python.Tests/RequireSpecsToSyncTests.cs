using DevTools.Execution.Providers.Python;

namespace DevTools.Execution.Tests;

[TestClass]
public sealed class RequireSpecsToSyncTests
{
    private static readonly Dictionary<string, string> Required = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mcp"] = ">=2.3.0,<3",
        ["debugpy"] = ">=1.8.22,<2",
    };

    [TestMethod]
    public void MatchingRequestedSpec_NeedsNoSync()
    {
        var json = """
            [
              {"name":"mcp","kind":"conda","requested_spec":">=2.3.0,<3"},
              {"name":"debugpy","kind":"conda","requested_spec":">=1.8.22,<2"},
              {"name":"numpy","kind":"conda","requested_spec":">=2"}
            ]
            """;

        var pending = PixiEnvironmentProvider.RequireSpecsToSync(json, Required);

        Assert.IsEmpty(pending);
    }

    [TestMethod]
    public void StaleOrMissingRequestedSpec_ReturnsRequireSpec()
    {
        var json = """
            [
              {"name":"mcp","kind":"pypi","requested_spec":">=2.1.1,<3"}
            ]
            """;

        var pending = PixiEnvironmentProvider.RequireSpecsToSync(json, Required);

        CollectionAssert.AreEquivalent(new[] { "mcp>=2.3.0,<3", "debugpy>=1.8.22,<2" }, pending);
    }

    [TestMethod]
    public void UnreadableList_SyncsEveryRequireSpec()
    {
        var pending = PixiEnvironmentProvider.RequireSpecsToSync("not-json", Required);

        CollectionAssert.AreEquivalent(Required.Select(pair => pair.Key + pair.Value).ToArray(), pending);
    }
}