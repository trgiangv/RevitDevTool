using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Scope;
using Nice3point.Revit.Extensions;
using RevitDevTool.Tools.ModelsContext.Filters;
using RevitRule = Autodesk.Revit.DB.FilterRule;
using RuleNode = DevTools.ModelsContext.Filters.FilterRule;

namespace RevitDevTool.ModelsContext.LiveTests;

[TestFixture]
public sealed class HostFilterTests : RecordedTest
{
    private static Adapter _adapter = null!;

    private Transaction? _edit;

    [OneTimeSetUp]
    public void Open()
    {
        _adapter = new Adapter();
    }

    [SetUp]
    public void Begin()
    {
        _edit = new Transaction(SampleModel.Host, "Model Context live test");
        _edit.Start();
    }

    [TearDown]
    public void RollBack()
    {
        if (_edit is { } edit && edit.HasStarted())
        {
            edit.RollBack();
        }

        _edit?.Dispose();
        _edit = null;
    }

    [Test]
    public void InvertedAndReadsAsOr()
    {
        Record(Case("HF-01"), () =>
        {
            var filter = HostFilterRevit.Create("mc-live inverted", HostFilterRevit.Inverted(HostFilterRevit.Contains(), HostFilterRevit.EqualsMark()));

            var root = HostFilterRevit.Read(_adapter, filter).Root;
            var rules = root.Children.Cast<RuleNode>().Select(rule => (rule.Operator.ToString(), rule.Value)).ToList();
            Evidence.Values(Describe(SetMatch.Any, HostFilterRevit.Data.InvertedReadBack.Select(rule => (rule.Operator, rule.Value))), Describe(root.Match, rules));

            Assert.That(root.Match, Is.EqualTo(SetMatch.Any));
            Assert.That(rules, Is.EqualTo(HostFilterRevit.Data.InvertedReadBack.Select(rule => (rule.Operator, rule.Value)).ToList()));
        });
    }

    [Test]
    public void InvertedReadCountsLikeRevit()
    {
        Record(Case("HF-02"), () =>
        {
            var filter = HostFilterRevit.Create("mc-live inverted count", HostFilterRevit.Inverted(HostFilterRevit.Contains(), HostFilterRevit.EqualsMark()));

            var native = Evidence.Time("oracle", () => HostFilterRevit.Native(filter));
            var count = Evidence.Time("panel", () => _adapter.Count(HostFilterRevit.Read(_adapter, filter), [SampleModel.Current], ScopeKind.WholeModel));
            Evidence.Step("Counted the read-back with the panel and the Revit ElementFilter with PassesFilter.");
            Evidence.Values(native, count);

            Assert.That(count, Is.EqualTo(native));
        });
    }

    [Test]
    public void InvertedWithoutNegationIsUnsupported()
    {
        Record(Case("HF-03"), () =>
        {
            var filter = HostFilterRevit.Create("mc-live inverted unsupported",
                HostFilterRevit.Inverted(HostFilterRevit.Contains(), ParameterFilterRuleFactory.CreateGreaterRule(HostFilterRevit.Reference, HostFilterRevit.ReferenceTarget)));

            var root = HostFilterRevit.Read(_adapter, filter).Root;
            Evidence.Values("one Unsupported rule", string.Join(", ", root.Children.Select(child => child.GetType().Name)));

            Assert.That(root.Children.Single(), Is.EqualTo(new UnsupportedRule(RevitFilterCatalog.InvertedRule)));
        });
    }

    [Test]
    public void ReferenceComparisonIsUnsupported()
    {
        Record(Case("HF-04"), () =>
        {
            var filter = HostFilterRevit.Create("mc-live reference greater",
                new ElementParameterFilter(ParameterFilterRuleFactory.CreateGreaterRule(HostFilterRevit.Reference, HostFilterRevit.ReferenceTarget)));

            var root = HostFilterRevit.Read(_adapter, filter).Root;
            Evidence.Values("Unsupported rule", string.Join(", ", root.Children.Select(child => child.GetType().Name)));

            Assert.That(root.Children.Single(), Is.InstanceOf<UnsupportedRule>());
        });
    }

    [Test]
    public void RuleFilterRunsNatively()
    {
        Record(Case("HF-05"), () =>
        {
            var filter = HostFilterRevit.Create("mc-live native", new ElementParameterFilter(HostFilterRevit.Contains()));

            var native = Evidence.Time("oracle", () => HostFilterRevit.Native(filter));
            var ids = Evidence.Time("panel", () => Adapter.Run(_adapter.HostFilters.EvaluateAsync(SampleModel.Current.Key, filter.Id.ToLong(), null,
                CancellationToken.None)));
            Evidence.Values(native, ids.Count);

            Assert.That(ids, Has.Count.EqualTo(native));
        });
    }

    [Test]
    public void SelectionFilterUsesStoredIds()
    {
        Record(Case("HF-06"), () =>
        {
            var stored = Oracle.Candidates(SampleModel.Host, [Cases.Category(HostFilterRevit.Data.Category)]).Take(HostFilterRevit.Data.SelectionCount).Select(element => element.Id).ToList();
            var selection = SelectionFilterElement.Create(SampleModel.Host, "mc-live selection");
            selection.SetElementIds(stored);
            Evidence.Step($"Created selection filter 'mc-live selection' with {stored.Count} ducts in a transaction that is rolled back.");

            var ids = Evidence.Time("panel", () => Adapter.Run(_adapter.HostFilters.EvaluateAsync(SampleModel.Current.Key, selection.Id.ToLong(), null,
                CancellationToken.None)));
            Evidence.Values(string.Join(", ", stored.Select(id => id.ToLong())), string.Join(", ", ids));

            Assert.That(ids.ToList(), Is.EquivalentTo(stored.Select(id => id.ToLong()).ToList()));
        });
    }

    [Test]
    public void CatalogListsBothKinds()
    {
        Record(Case("HF-07"), () =>
        {
            var rule = HostFilterRevit.Create("mc-live listed", new ElementParameterFilter(HostFilterRevit.Contains()));
            var selection = SelectionFilterElement.Create(SampleModel.Host, "mc-live listed selection");

            var filters = Evidence.Time("panel", () => Adapter.Run(_adapter.HostFilters.GetFiltersAsync(SampleModel.Current.Key, CancellationToken.None)));
            var ruleKind = filters.Single(filter => filter.Id == rule.Id.ToLong()).Kind;
            var selectionKind = filters.Single(filter => filter.Id == selection.Id.ToLong()).Kind;
            Evidence.Values("RuleBased, Selection", $"{ruleKind}, {selectionKind}");

            Assert.That(ruleKind, Is.EqualTo(HostFilterKind.RuleBased));
            Assert.That(selectionKind, Is.EqualTo(HostFilterKind.Selection));
        });
    }

    private static CaseItem Case(string id)
    {
        return Areas.Find(Areas.HostFilters.Cases, id);
    }

    private static string Describe(SetMatch match, IEnumerable<(string Operator, string Value)> rules)
    {
        return $"{match}: " + string.Join(", ", rules.Select(rule => $"{rule.Operator} '{rule.Value}'"));
    }
}

internal static class HostFilterRevit
{
    public static HostFilterSetup Data => Areas.HostFilters.Setup;

    public static ElementId Text => new(Keys.BuiltInOf(Data.Text.Parameter));

    public static ElementId Mark => new(Keys.BuiltInOf(Data.Mark.Parameter));

    public static ElementId Reference => new(Keys.BuiltInOf(Data.Reference.Parameter));

    public static List<ElementId> Categories => [Cases.Category(Data.Category).ToElementId()];

    public static ElementId ReferenceTarget => new FilteredElementCollector(SampleModel.Host)
        .OfClass(typeof(Element).Assembly.GetType("Autodesk.Revit.DB." + Data.Reference.TargetClass, true)!)
        .FirstElementId();

    public static RevitRule Contains()
    {
        return ParameterFilterRuleFactory.CreateContainsRule(Text, Data.Text.Value);
    }

    public static RevitRule EqualsMark()
    {
        return ParameterFilterRuleFactory.CreateEqualsRule(Mark, Data.Mark.Value);
    }

    public static ElementParameterFilter Inverted(params RevitRule[] rules)
    {
        return new ElementParameterFilter(rules.ToList(), true);
    }

    public static ParameterFilterElement Create(string name, ElementFilter filter)
    {
        Evidence.Step($"Created Revit filter '{name}' on {Data.Category} in a transaction that is rolled back after the test.");
        return ParameterFilterElement.Create(SampleModel.Host, name, Categories, filter);
    }

    public static FilterDefinition Read(Adapter adapter, ParameterFilterElement filter)
    {
        var definition = Adapter.Run(adapter.HostFilters.ReadAsync(SampleModel.Current.Key, filter.Id.ToLong(), CancellationToken.None));
        Evidence.Step($"Read '{filter.Name}' back through RevitFilterCatalog.ReadAsync.");
        Assert.That(definition, Is.Not.Null);
        Assert.That(definition!.Groups.ToList(), Is.EqualTo(new[] { Cases.Category(Data.Category) }));
        return definition!;
    }

    public static int Native(ParameterFilterElement filter)
    {
        var elementFilter = filter.GetElementFilter();
        return Oracle.Candidates(SampleModel.Host, [Cases.Category(Data.Category)]).Count(elementFilter.PassesFilter);
    }
}
