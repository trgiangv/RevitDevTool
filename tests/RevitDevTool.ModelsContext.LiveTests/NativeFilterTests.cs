using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Parameters;
using DevTools.ModelsContext.Scope;
using System.Text.Json;
using System.Text.Json.Serialization;
using RuleNode = DevTools.ModelsContext.Filters.FilterRule;

namespace RevitDevTool.ModelsContext.LiveTests;

internal sealed record NativeRow(
    string Id,
    string Description,
    string Docs,
    string Scope,
    int Panel,
    int Oracle,
    int? Native,
    bool? NativeEqualsPanel,
    string AcceptedAsViewFilter,
    int? ViewFilterCount,
    List<string> Reasons,
    string Note);

[TestFixture]
public sealed class NativeFilterTests : RecordedTest
{
    private const string SummaryFile = "native-comparison.json";

    private static readonly JsonSerializerOptions SummaryOptions = new(CaseFiles.Options)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    private static readonly List<NativeRow> Rows = [];
    private static Adapter _adapter = null!;

    public static IEnumerable<TestCaseData> Matrix => Areas.Rows(CaseFiles.Filters);

    [OneTimeSetUp]
    public void Open()
    {
        _adapter = new Adapter();
        Rows.Clear();
    }

    [TestCaseSource(nameof(Matrix))]
    public void CompareWithNative(string id)
    {
        var item = CaseFiles.Filter(id);
        Record(item, () =>
        {
            var documents = Prepare(item);
            var definition = Cases.Definition(item, (key, groups, text) => Picks.Pick(_adapter, documents, key, groups, text));
            var scope = Cases.Scope(item);
            using var units = HostUnits.Set(item.HostLengthUnit);

            var oracle = Evidence.Time("oracle", () => Oracle.Count(definition, documents, scope));
            var panel = Evidence.Time("panel", () => _adapter.Count(definition, documents, scope));
            var native = Evidence.Time("native", () => NativeFilter.Run(definition, documents, scope));
            Evidence.Step("Native: the same rules built with ParameterFilterRuleFactory, ElementParameterFilter and ElementMulticategoryFilter and collected on each document directly (case-insensitive strings, no type-parameter or empty-text handling, any design option).");
            Evidence.Values(oracle, panel);

            var reasons = Reasons(item, definition, documents, native, panel);
            var row = new NativeRow(item.Id, item.Description, item.Docs, item.Scope, panel, oracle, native.Count,
                native.Count is { } count ? count == panel : null, native.Accepted, native.ViewFilterCount, reasons, native.Note);
            Rows.Add(row);
            WriteSummary();
            Evidence.Step(Describe(row));

            Assert.That(panel, Is.EqualTo(oracle), $"{id}: the panel counts {panel}, the oracle {oracle}. Panel bug.");
        });
    }

    private static string Describe(NativeRow row)
    {
        var native = row.Native is { } count ? count.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) : "not expressible";
        return $"Panel {row.Panel:N0}, oracle {row.Oracle:N0}, native {native}, accepted as a view filter: {row.AcceptedAsViewFilter}"
               + (row.ViewFilterCount is { } view ? $" (collects {view:N0})" : string.Empty)
               + $", difference: {string.Join(", ", row.Reasons)}. {row.Note}";
    }

    private static List<string> Reasons(FilterCaseData item, FilterDefinition definition, IReadOnlyList<SampleDocument> documents,
        NativeOutcome native, int panel)
    {
        if (native.Count is null)
        {
            return [native.Reason];
        }

        if (native.Count == panel)
        {
            return ["none"];
        }

        var catalogs = documents.Select(document => Picks.Catalog(_adapter, document)).ToList();
        var rules = RulesOf(definition.Root).ToList();
        var reasons = new List<string>();
        ParameterInfo? Info(ParameterKey key)
        {
            return catalogs.SelectMany(catalog => catalog).FirstOrDefault(info => info.Key.Equals(key));
        }

        if (rules.Any(rule => rule.IsCaseSensitive))
        {
            reasons.Add("case");
        }

        if (rules.Any(rule => Info(rule.Parameter) is { } info && (info.Bindings & ParameterBinding.Type) != 0))
        {
            reasons.Add("type parameter");
        }

        if (rules.Any(rule => Info(rule.Parameter)?.Kind == ValueKind.Text
                              && (rule.Operator is FilterOperator.Empty or FilterOperator.NotEmpty
                                  || (rule.Operator == FilterOperator.Equal && rule.Value.Length == 0))))
        {
            reasons.Add("empty text");
        }

        if (rules.Any(rule => Info(rule.Parameter)?.Kind == ValueKind.Text && TextRules.IsOrder(rule.Operator)))
        {
            reasons.Add("text order");
        }

        if (documents.Any(document => Oracle.Candidates(document.Document, definition.Groups, true).Any(element => !Oracle.InMainOrPrimary(element))))
        {
            reasons.Add("design option");
        }

        if (documents.Any(document => !document.Key.IsCurrentModel)
            && (item.HostLengthUnit is not null || rules.Any(rule => rule.Picked is not null || Info(rule.Parameter)?.Kind == ValueKind.Number)))
        {
            reasons.Add("link");
        }

        if (rules.Any(rule => catalogs.Any(catalog => catalog.All(info => !info.Key.Equals(rule.Parameter)))))
        {
            reasons.Add("missing parameter");
        }

        if (reasons.Count == 0)
        {
            reasons.Add("unexplained");
        }

        return reasons;
    }

    private static IEnumerable<RuleNode> RulesOf(FilterNode node)
    {
        return node switch
        {
            RuleNode rule => [rule],
            FilterSet set => set.Children.SelectMany(RulesOf),
            _ => []
        };
    }

    private static void WriteSummary()
    {
        var differing = Rows.Where(row => row.NativeEqualsPanel == false).ToList();
        var summary = new
        {
            run = Path.GetFileName(Evidence.Folder),
            cases = Rows.Count,
            nativeComputed = Rows.Count(row => row.Native is not null),
            nativeEqualsPanel = Rows.Count(row => row.NativeEqualsPanel == true),
            nativeDiffersFromPanel = differing.Count,
            notExpressible = Rows.Count(row => row.Native is null),
            acceptedAsViewFilterYes = Rows.Count(row => row.AcceptedAsViewFilter == NativeFilter.Yes),
            acceptedAsViewFilterNo = Rows.Count(row => row.AcceptedAsViewFilter == NativeFilter.No),
            acceptedAsViewFilterNotApplicable = Rows.Count(row => row.AcceptedAsViewFilter == NativeFilter.NotApplicable),
            byReason = Rows.Where(row => row.Native is null || row.NativeEqualsPanel == false)
                .SelectMany(row => row.Reasons).GroupBy(reason => reason).OrderBy(group => group.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count()),
            rows = Rows
        };
        File.WriteAllText(Path.Combine(Evidence.Folder, SummaryFile), JsonSerializer.Serialize(summary, SummaryOptions));
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
