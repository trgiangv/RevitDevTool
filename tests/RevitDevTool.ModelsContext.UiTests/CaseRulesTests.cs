using RevitDevTool.ModelsContext.LiveTests;
using System.Text.Json;

namespace RevitDevTool.ModelsContext.UiTests;

[TestClass]
public sealed class CaseRulesTests
{
    private static readonly string[] ImageFiles = ["visibility.json", "ui-flows.json"];
    private static readonly string[] RequiredImages = ["before", "after"];

    [TestMethod]
    public void EveryCaseIsComplete()
    {
        var problems = new List<string>();
        foreach (var (file, item) in AllCases())
        {
            var id = Text(item, "id");
            if (string.IsNullOrWhiteSpace(id))
            {
                problems.Add($"{file}: a case has no id.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(Text(item, "description")))
            {
                problems.Add($"{file} {id}: no description.");
            }

            if (!item.TryGetProperty("expected", out var expected) || expected.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                problems.Add($"{file} {id}: no expected outcome.");
            }

            if (!item.TryGetProperty("manualSteps", out var steps) || steps.ValueKind != JsonValueKind.Array
                || steps.GetArrayLength() == 0 || steps.EnumerateArray().Any(step => string.IsNullOrWhiteSpace(step.GetString())))
            {
                problems.Add($"{file} {id}: no manual steps (\"manualSteps\": [\"...\"]).");
            }
        }

        Assert.IsEmpty(problems, string.Join(Environment.NewLine, problems));
    }

    [TestMethod]
    public void CaseIdsAreUnique()
    {
        var duplicates = AllCases().GroupBy(pair => Text(pair.Item, "id")).Where(group => group.Count() > 1)
            .Select(group => $"{group.Key} in {string.Join(", ", group.Select(pair => pair.File))}").ToList();

        Assert.IsEmpty(duplicates, string.Join(Environment.NewLine, duplicates));
    }

    [TestMethod]
    public void ActionCasesDeclareImages()
    {
        var problems = new List<string>();
        foreach (var (file, item) in AllCases().Where(pair => ImageFiles.Contains(pair.File)))
        {
            var images = item.TryGetProperty("images", out var array) && array.ValueKind == JsonValueKind.Array
                ? array.EnumerateArray().Select(image => image.GetString()).ToList()
                : [];
            var missing = RequiredImages.Where(label => !images.Contains(label)).ToList();
            if (missing.Count > 0)
            {
                problems.Add($"{file} {Text(item, "id")}: image slots missing: {string.Join(", ", missing)}.");
            }
        }

        Assert.IsEmpty(problems, string.Join(Environment.NewLine, problems));
    }

    [TestMethod]
    public void LinkTiersArePlanOnly()
    {
        var problems = AllCases().Where(pair => pair.File == "visibility.json" && Text(pair.Item, "tier").Contains("T3", StringComparison.Ordinal))
            .Where(pair => !pair.Item.TryGetProperty("expected", out var expected)
                           || !(expected.TryGetProperty("plannedTiers", out _) || expected.TryGetProperty("error", out _)))
            .Select(pair => Text(pair.Item, "id"))
            .ToList();

        Assert.IsEmpty(problems, "T3 cases run Hide in View through an internal Revit command, which crashed Revit when run from the test runner. Give them expected.plannedTiers (or an expected error): " + string.Join(", ", problems));
    }

    [TestMethod]
    public void FilterCasesHaveCounts()
    {
        var missing = CaseFiles.Filters.Where(item => item.ExpectedCount is null).Select(item => item.Id).ToList();

        Assert.IsEmpty(missing, "Filter cases need a number as expected value (from the oracle): " + string.Join(", ", missing));
    }

    [TestMethod]
    public void FlowsNameKnownCases()
    {
        var known = CaseFiles.Filters.Select(item => item.Id).ToHashSet();
        var unknown = CaseFiles.Flows.SelectMany(flow => flow.Cases.Where(id => !known.Contains(id)).Select(id => $"{flow.Id} -> {id}")).ToList();

        Assert.IsEmpty(unknown, string.Join(", ", unknown));
    }

    private static IEnumerable<(string File, JsonElement Item)> AllCases()
    {
        foreach (var path in Directory.GetFiles(CaseFiles.Folder, "*.json").OrderBy(path => path, StringComparer.Ordinal))
        {
            using var json = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
            if (json.RootElement.ValueKind != JsonValueKind.Object || !json.RootElement.TryGetProperty("cases", out var cases))
            {
                continue;
            }

            foreach (var item in cases.EnumerateArray())
            {
                yield return (Path.GetFileName(path), item.Clone());
            }
        }
    }

    private static string Text(JsonElement item, string name)
    {
        return item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
    }
}
