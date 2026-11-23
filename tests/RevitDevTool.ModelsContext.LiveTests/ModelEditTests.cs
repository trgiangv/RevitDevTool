using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Scope;

namespace RevitDevTool.ModelsContext.LiveTests;

[TestFixture]
public sealed class ModelEditTests : RecordedTest
{
    private Transaction? _edit;

    private static EditSetup Data => Areas.Edits.Setup;

    [SetUp]
    public void Begin()
    {
        _edit = new Transaction(SampleModel.Host, "Model Context live edit");
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
    public void ValueEditRecounts()
    {
        Record(Areas.Find(Areas.Edits.Cases, "LV-03"), () =>
        {
            var adapter = new Adapter();
            var key = Keys.Of(Data.Parameter);
            var definition = new FilterDefinition([Cases.Category(Data.Category)],
                FilterSet.All(new DevTools.ModelsContext.Filters.FilterRule(key, FilterOperator.Equal, Data.Value)));
            var before = Evidence.Time("panel", () => adapter.Count(definition, [SampleModel.Current], ScopeKind.WholeModel));

            foreach (var element in Elements().Take(Data.Edited))
            {
                Oracle.Find(element, key)!.Set(Data.Value);
            }

            Evidence.Step($"Set {Data.Parameter} = '{Data.Value}' on {Data.Edited} ducts in a transaction that is rolled back.");
            adapter.Cache.InvalidateValues(SampleModel.Current.Source);
            var after = Evidence.Time("panel", () => adapter.Count(definition, [SampleModel.Current], ScopeKind.WholeModel));
            var oracle = Evidence.Time("oracle", () => Oracle.Count(definition, [SampleModel.Current], ScopeKind.WholeModel));
            Evidence.Step("Dropped the value cache as DocumentChanged does and counted again.");
            Evidence.Values($"0 before, {oracle} after", $"{before} before, {after} after");

            Assert.That((before, after), Is.EqualTo((0, Data.Edited)));
            Assert.That(after, Is.EqualTo(oracle));
        });
    }

    [Test]
    public void AddedElementCountsAfterRefresh()
    {
        Record(Areas.Find(Areas.Edits.Cases, "LV-04"), () =>
        {
            var adapter = new Adapter();
            var definition = new FilterDefinition([Cases.Category(Data.Category)], FilterSet.All());
            var before = Evidence.Time("panel", () => adapter.Count(definition, [SampleModel.Current], ScopeKind.WholeModel));

            ElementTransformUtils.CopyElement(SampleModel.Host, Elements().First().Id, new XYZ(0, 0, Data.CopyOffsetFeet));
            Evidence.Step($"Copied one duct {Data.CopyOffsetFeet} ft up in a transaction that is rolled back.");
            var stale = adapter.Count(definition, [SampleModel.Current], ScopeKind.WholeModel);
            adapter.Cache.InvalidateValues(SampleModel.Current.Source);
            var after = Evidence.Time("panel", () => adapter.Count(definition, [SampleModel.Current], ScopeKind.WholeModel));
            var oracle = Evidence.Time("oracle", () => Oracle.Count(definition, [SampleModel.Current], ScopeKind.WholeModel));
            Evidence.Values($"{before}, {before}, then {oracle}", $"{before}, {stale}, then {after}");

            Assert.That((stale, after), Is.EqualTo((before, before + 1)));
            Assert.That(after, Is.EqualTo(oracle));
        });
    }

    private static IEnumerable<Element> Elements()
    {
        return Oracle.Candidates(SampleModel.Host, [Cases.Category(Data.Category)]);
    }
}
