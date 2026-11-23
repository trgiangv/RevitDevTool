using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Parameters;
using DevTools.ModelsContext.Scope;
using Nice3point.Revit.Extensions;
using System.Text;
using RuleNode = DevTools.ModelsContext.Filters.FilterRule;

namespace RevitDevTool.ModelsContext.LiveTests;

[TestFixture]
public sealed class FilterMatrixTests : RecordedTest
{
    private static Adapter _adapter = null!;

    public static IEnumerable<TestCaseData> Matrix => Areas.Rows(CaseFiles.Filters);

    [OneTimeSetUp]
    public void Open()
    {
        _adapter = new Adapter();
    }

    [TestCaseSource(nameof(Matrix))]
    public void CountMatchesOracle(string id)
    {
        var item = CaseFiles.Filter(id);
        Record(item, () =>
        {
            var documents = Prepare(item);
            var definition = Cases.Definition(item, (key, groups, text) => Pick(documents, key, groups, text));
            Evidence.Step($"Built the filter from {item.Id} in filters.json/scopes.json: {item.Description}.");
            using var units = HostUnits.Set(item.HostLengthUnit);
            if (item.HostLengthUnit is { } unit)
            {
                Evidence.Step($"Set the Current model's length unit to {unit} in a transaction that is rolled back after the count.");
            }

            var oracle = Evidence.Time("oracle", () => Oracle.Count(definition, documents, Cases.Scope(item)));
            Evidence.Step($"Oracle: counted with a plain FilteredElementCollector and Parameter reads in {Names(documents)}, scope {item.Scope}.");
            var panel = Evidence.Time("panel", () => _adapter.Count(definition, documents, Cases.Scope(item)));
            Evidence.Step("Panel: counted with the adapter the panel uses (RevitFilterEvaluator, RevitModelSource).");
            Evidence.Values(oracle, panel);

            Assert.That(item.ExpectedCount, Is.Not.Null, $"{id} has no expected value yet; the oracle counts {oracle}.");
            Assert.That(oracle, Is.EqualTo(item.ExpectedCount), $"{id}: the model no longer gives the expected value (oracle).");
            Assert.That(panel, Is.EqualTo(oracle), $"{id}: the panel counts {panel}, the oracle {oracle}. Panel bug; keep the expected value.");
        });
    }

    private static NumberRange? Pick(IReadOnlyList<SampleDocument> documents, ParameterKey key, IReadOnlyList<long> groups, string text)
    {
        NumberRange? picked = null;
        foreach (var source in documents.Select(document => document.Source).Distinct())
        {
            var counts = Adapter.Run(_adapter.Evaluator.CountValuesAsync(source, key, groups, CancellationToken.None));
            if (counts.Numbers is { } numbers && numbers.TryGetValue(text, out var range))
            {
                picked = picked is { } known ? known.Union(range) : range;
            }
        }

        if (picked is { } found)
        {
            Evidence.Step($"Picked '{text}' from the value list of '{key.Name}': stored {found.Min:R} to {found.Max:R}.");
        }

        return picked;
    }

    [Test]
    public void WriteMismatchedElements()
    {
        var text = new StringBuilder();
        foreach (var item in CaseFiles.Filters.Where(item => Cases.Scope(item) == ScopeKind.WholeModel))
        {
            var documents = SampleModel.Of(item.Docs);
            var definition = Cases.Definition(item, (key, groups, text) => Pick(documents, key, groups, text));
            using var units = HostUnits.Set(item.HostLengthUnit);
            var oracle = Oracle.Matches(definition, documents, ScopeKind.WholeModel).ToHashSet();
            var panel = _adapter.Matches(definition, documents, ScopeKind.WholeModel).ToHashSet();
            if (oracle.SetEquals(panel))
            {
                continue;
            }

            var keys = ParametersOf(definition.Root).ToList();
            text.AppendLine($"## {item.Id} {item.Description}: oracle {oracle.Count}, panel {panel.Count}");
            foreach (var (side, element) in oracle.Except(panel).Select(element => ("oracle only", element))
                         .Concat(panel.Except(oracle).Select(element => ("panel only", element))).Take(40))
            {
                var document = documents.First(candidate => candidate.Key == element.Document).Document;
                var target = document.GetElement(element.Id.ToElementId());
                var values = keys.Select(key => $"{key.Name}: {Describe(Oracle.Read(target, key))}");
                text.AppendLine($"- {side}: {element.Id} {target.Category?.Name} '{target.Name}' | {string.Join(" | ", values)}");
            }

            text.AppendLine();
        }

        File.WriteAllText(Path.Combine(Evidence.Folder, "mismatched-elements.md"), text.Length == 0 ? "Oracle and panel match on every row.\n" : text.ToString());
    }

    private static IEnumerable<ParameterKey> ParametersOf(FilterNode node)
    {
        return node switch
        {
            RuleNode rule => [rule.Parameter],
            FilterSet set => set.Children.SelectMany(ParametersOf).Distinct(),
            _ => []
        };
    }
    private static string Names(IEnumerable<SampleDocument> documents)
    {
        return string.Join(" and ", documents.Select(document => document.Document.Title));
    }

    private static string Describe((Parameter? Instance, Parameter? Type) read)
    {
        static string One(Parameter? parameter)
        {
            return parameter is null
                ? "none"
                : $"has={parameter.HasValue} text='{parameter.AsValueString()}' string='{parameter.AsString()}' int={(parameter.StorageType == StorageType.Integer ? parameter.AsInteger() : 0)}";
        }

        return $"instance {One(read.Instance)}; type {One(read.Type)}";
    }

    private static IReadOnlyList<SampleDocument> Prepare(FilterCaseData item)
    {
        if (Cases.Scope(item) == ScopeKind.ActiveView)
        {
            SampleViews.Activate();
            Evidence.Step($"Made '{CaseFiles.Facts.ViewName}' the active view.");
        }

        return SampleModel.Of(item.Docs);
    }
}
