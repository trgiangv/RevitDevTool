using DevTools.ModelsContext.Model;
using Nice3point.Revit.Extensions;
using RevitDevTool.Tools.ModelsContext;

namespace RevitDevTool.ModelsContext.LiveTests;

[TestFixture]
public sealed class ExplorerCountTests : RecordedTest
{
    private static Adapter _adapter = null!;

    public static IEnumerable<TestCaseData> Rows => Areas.Rows(Areas.Explorer);

    [OneTimeSetUp]
    public void Open()
    {
        _adapter = new Adapter();
    }

    [TestCaseSource(nameof(Rows))]
    public void ExplorerMatchesOracle(string id)
    {
        var item = Areas.Find(Areas.Explorer, id);
        Record(item, () =>
        {
            switch (item.Check)
            {
                case "CategoryCount":
                    CategoryCount(item);
                    break;
                case "SnapshotPerCategory":
                    PerDocument(item, SnapshotPerCategory);
                    break;
                case "QuickCounts":
                    PerDocument(item, QuickCounts);
                    break;
                case "ViewCounts":
                    PerDocument(item, ViewCounts);
                    break;
                case "ViewIds":
                    PerDocument(item, ViewIds);
                    break;
                case "SecondaryOptions":
                    PerDocument(item, SecondaryOptions);
                    break;
                case "DocumentList":
                    DocumentList();
                    break;
                default:
                    Assert.Fail($"{id}: unknown check '{item.Check}' in explorer.json.");
                    break;
            }
        });
    }

    private static void CategoryCount(ExplorerCase item)
    {
        var document = SampleModel.Of(item.Docs)[0];
        var id = Cases.Category(item.Category!);
        var oracle = Evidence.Time("oracle", () => OracleCounts(document).TryGetValue(id, out var count) ? count : 0);
        var snapshot = Evidence.Time("panel", () => Adapter.Run(_adapter.Source.BuildSnapshotAsync(document.Source, null, CancellationToken.None)));
        var panel = snapshot.Rows.Count(row => !row.SecondaryOption && snapshot.Groups[row.GroupIndex].GroupId == id);
        Evidence.Step($"Built the Explorer snapshot of {document.Document.Title} and counted the {item.Category} rows.");
        Evidence.Values(oracle, panel);

        Assert.That(oracle, Is.EqualTo(item.ExpectedCount), "oracle");
        Assert.That(panel, Is.EqualTo(oracle), "panel");
    }

    private static void PerDocument(ExplorerCase item, Func<SampleDocument, (string Oracle, string Panel, bool Equal)> check)
    {
        var oracle = new List<string>();
        var panel = new List<string>();
        var different = new List<string>();
        foreach (var docs in item.Docs.Split(','))
        {
            var document = SampleModel.Of(docs.Trim())[0];
            var (oracleText, panelText, equal) = check(document);
            oracle.Add($"{document.Label}: {oracleText}");
            panel.Add($"{document.Label}: {panelText}");
            if (!equal)
            {
                different.Add(document.Label);
            }
        }

        Evidence.Values(string.Join("; ", oracle), string.Join("; ", panel));
        Assert.That(different, Is.Empty, $"{item.Id}: panel and oracle differ in {string.Join(", ", different)}.");
    }

    private static (string, string, bool) SnapshotPerCategory(SampleDocument document)
    {
        var oracle = Evidence.Time("oracle", () => OracleCounts(document));
        var snapshot = Evidence.Time("panel", () => Adapter.Run(_adapter.Source.BuildSnapshotAsync(document.Source, null, CancellationToken.None)));
        var panel = snapshot.Rows.Where(row => !row.SecondaryOption).GroupBy(row => snapshot.Groups[row.GroupIndex].GroupId)
            .ToDictionary(group => group.Key, group => group.Count());
        Evidence.Step($"{document.Document.Title}: compared the snapshot count of each category with the oracle.");
        return Compare(oracle, panel);
    }

    private static (string, string, bool) QuickCounts(SampleDocument document)
    {
        var oracle = Evidence.Time("oracle", () => OracleCounts(document));
        var quick = Evidence.Time("panel", () => Adapter.Run(_adapter.Source.CountGroupsAsync(document.Key, ScopeKind.WholeModel, CancellationToken.None)));
        Evidence.Step($"{document.Document.Title}: compared the quick count of each category with the oracle.");
        return Compare(oracle, quick.ToDictionary(group => group.GroupId, group => group.Count));
    }

    private static (string, string, bool) ViewCounts(SampleDocument document)
    {
        SampleViews.Activate();
        var oracle = Evidence.Time("oracle", () =>
        {
            var view = Oracle.ViewIds(document);
            return Oracle.Candidates(document.Document, [], true)
                .Where(element => view.Contains(element.Id.ToLong()))
                .GroupBy(element => element.Category.Id.ToLong())
                .ToDictionary(group => group.Key, group => group.Count());
        });
        var quick = Evidence.Time("panel", () => Adapter.Run(_adapter.Source.CountGroupsAsync(document.Key, ScopeKind.ActiveView, CancellationToken.None)));
        Evidence.Step($"{document.Document.Title}: compared Active view counts in '{CaseFiles.Facts.ViewName}' with the view collector.");
        return Compare(oracle, quick.ToDictionary(group => group.GroupId, group => group.Count));
    }

    private static (string, string, bool) ViewIds(SampleDocument document)
    {
        SampleViews.Activate();
        var oracle = Evidence.Time("oracle", () => Oracle.ViewIds(document));
        var ids = Evidence.Time("panel", () => Adapter.Run(_adapter.Source.CollectScopeAsync(document.Key, ScopeKind.ActiveView, CancellationToken.None)));
        Evidence.Step($"{document.Document.Title}: compared the Active view id set with the view collector.");
        var equal = oracle.SetEquals(ids);
        return ($"{oracle.Count} ids", $"{ids.Count} ids{(equal ? "" : ", different set")}", equal);
    }

    private static (string, string, bool) SecondaryOptions(SampleDocument document)
    {
        var secondary = new FilteredElementCollector(document.Document)
            .WhereElementIsNotElementType()
            .Count(element => element.DesignOption is { IsPrimary: false } && element.Category is not null);
        if (secondary == 0)
        {
            Assert.Inconclusive($"{document.Label} has no elements in secondary design options.");
        }

        var oracle = Evidence.Time("oracle", () => OracleCounts(document).Values.Sum());
        var snapshot = Evidence.Time("panel", () => Adapter.Run(_adapter.Source.BuildSnapshotAsync(document.Source, null, CancellationToken.None)));
        Evidence.Step($"{document.Document.Title}: {secondary} elements sit in secondary design options; compared the snapshot count with main + primary.");
        return ($"{oracle:N0} (main + primary)", $"{snapshot.Counts.Count:N0}", snapshot.Counts.Count == oracle);
    }

    private static void DocumentList()
    {
        var host = SampleModel.Host;
        var expected = new List<DocumentKey> { SampleModel.Current.Key };
        expected.AddRange(new FilteredElementCollector(host).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>()
            .Select(instance => DocumentRegistry.KeyOf(host, instance)));
        var documents = Evidence.Time("panel", () => Adapter.Run(_adapter.Source.GetDocumentsAsync(CancellationToken.None)));
        Evidence.Step("Listed the documents the panel shows and the model's loaded link instances.");
        Evidence.Values(string.Join(", ", SampleModel.All.Select(document => document.Label)),
            string.Join(", ", documents.Select(document => document.Source.Label)));

        Assert.That(documents.Select(document => document.Key).ToList(), Is.EquivalentTo(expected));
        Assert.That(documents[0].Key.IsCurrentModel, Is.True, "The Current model is not first.");
    }

    private static (string, string, bool) Compare(Dictionary<long, int> oracle, Dictionary<long, int> panel)
    {
        var keys = oracle.Keys.Union(panel.Keys).ToList();
        var different = keys.Where(key => !oracle.TryGetValue(key, out var left) || !panel.TryGetValue(key, out var right) || left != right).ToList();
        var detail = different.Count == 0 ? "" : " differ: " + string.Join(", ", different.Take(5).Select(key =>
            $"{CategoryName(key)} {(oracle.TryGetValue(key, out var left) ? left : 0)} vs {(panel.TryGetValue(key, out var right) ? right : 0)}"));
        return ($"{oracle.Count} categories, {oracle.Values.Sum():N0} elements",
            $"{panel.Count} categories, {panel.Values.Sum():N0} elements{detail}", different.Count == 0);
    }

    private static string CategoryName(long id)
    {
        return Category.GetCategory(SampleModel.Host, id.ToElementId())?.Name ?? id.ToString();
    }

    private static Dictionary<long, int> OracleCounts(SampleDocument document)
    {
        return Oracle.Candidates(document.Document, [])
            .GroupBy(element => element.Category.Id.ToLong())
            .ToDictionary(group => group.Key, group => group.Count());
    }
}
