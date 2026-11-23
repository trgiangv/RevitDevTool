using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Explorer;
using DevTools.ModelsContext.Scope;
using DevTools.ModelsContext.Tests.Shared;
using DevTools.ModelsContext.Visibility;
using System.Globalization;

namespace DevTools.ModelsContext.Tests.Explorer;

[TestClass]
public sealed class HiddenRowsTests
{
    private static readonly DocumentKey Current = FakeModelGenerator.Current;

    [TestMethod]
    public async Task GroupCountsHiddenElements()
    {
        using var fixture = await PanelFixture.OpenAsync(scope: ScopeKind.ActiveView);
        var snapshot = TestModel.Snapshot(Current);
        var total = snapshot.Counts.CountAt(snapshot.GroupIndexOf(TestModel.Ducts));

        var ducts = fixture.Group(Current, "Ducts");

        Assert.IsGreaterThan(0, ducts.HiddenCount);
        Assert.AreEqual(total - ducts.Count, ducts.HiddenCount);
        Assert.AreEqual(string.Format(CultureInfo.CurrentCulture, "+{0:N0} hidden", ducts.HiddenCount), ducts.HiddenText);
    }

    [TestMethod]
    public async Task WholeModelHasNoHidden()
    {
        using var fixture = await PanelFixture.OpenAsync();

        Assert.IsTrue(fixture.Explorer.Rows.All(row => row.HiddenCount == 0));
    }

    [TestMethod]
    public async Task HiddenClickListsDimmedRows()
    {
        using var fixture = await PanelFixture.OpenAsync(scope: ScopeKind.ActiveView);
        var type = await HiddenTypeAsync(fixture);

        await fixture.Explorer.ShowHiddenCommand.ExecuteAsync(type);

        Assert.IsTrue(type.IsHiddenOpen);
        Assert.IsTrue(fixture.Explorer.Rows.Any(row => row.Parent == type && row.IsDimmed));
        Assert.IsTrue(fixture.Explorer.Rows.Any(row => row.Parent == type && !row.IsDimmed));
    }

    [TestMethod]
    public async Task HiddenClickAgainCloses()
    {
        using var fixture = await PanelFixture.OpenAsync(scope: ScopeKind.ActiveView);
        var type = await HiddenTypeAsync(fixture);
        await fixture.Explorer.ShowHiddenCommand.ExecuteAsync(type);

        await fixture.Explorer.ShowHiddenCommand.ExecuteAsync(type);

        Assert.IsFalse(type.IsHiddenOpen);
        Assert.IsFalse(fixture.Explorer.Rows.Any(row => row.IsDimmed));
    }

    [TestMethod]
    public async Task GroupSelectSkipsHidden()
    {
        using var fixture = await PanelFixture.OpenAsync(scope: ScopeKind.ActiveView);
        var ducts = fixture.Group(Current, "Ducts");

        var elements = fixture.Explorer.ElementsOf([ducts]);

        Assert.HasCount(ducts.Count, elements);
    }

    [TestMethod]
    public async Task GroupHideSkipsHidden()
    {
        using var fixture = await PanelFixture.OpenAsync(scope: ScopeKind.ActiveView);
        var ducts = fixture.Group(Current, "Ducts");
        var visible = ducts.Count;

        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(ducts);

        var step = (ElementsStep)fixture.Actions.ExecutedPlans.Single().Steps.Single();
        Assert.HasCount(visible, step.Ids);
    }

    [TestMethod]
    public async Task DimmedRowActsAlone()
    {
        using var fixture = await PanelFixture.OpenAsync(scope: ScopeKind.ActiveView);
        var type = await HiddenTypeAsync(fixture);
        await fixture.Explorer.ShowHiddenCommand.ExecuteAsync(type);
        var dimmed = fixture.Explorer.Rows.First(row => row.Parent == type && row.IsDimmed);

        var elements = fixture.Explorer.ElementsOf([dimmed]);

        CollectionAssert.AreEqual(new[] { new ElementRef(Current, dimmed.Id) }, elements.ToList());
    }

    [TestMethod]
    public async Task DimmedRowSetsWhyElement()
    {
        using var fixture = await PanelFixture.OpenAsync(scope: ScopeKind.ActiveView);
        var type = await HiddenTypeAsync(fixture);
        await fixture.Explorer.ShowHiddenCommand.ExecuteAsync(type);
        var dimmed = fixture.Explorer.Rows.First(row => row.Parent == type && row.IsDimmed);
        var shown = fixture.Explorer.Rows.First(row => row.Parent == type && row is { Kind: TreeRowKind.Instance, IsDimmed: false });
        var changes = new List<string?>();
        fixture.Explorer.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        fixture.Explorer.SetSelectedRows([dimmed]);
        var picked = fixture.Explorer.SelectedDimmedElement;
        fixture.Explorer.SetSelectedRows([shown]);

        Assert.AreEqual(new ElementRef(Current, dimmed.Id), picked);
        Assert.IsNull(fixture.Explorer.SelectedDimmedElement);
        Assert.AreEqual(2, changes.Count(name => name == nameof(ExplorerTabViewModel.SelectedDimmedElement)));
    }

    [TestMethod]
    public async Task SearchFindsHiddenElement()
    {
        using var fixture = await PanelFixture.OpenAsync(scope: ScopeKind.ActiveView);
        var id = fixture.Model.Data.SourceOf(Current).Elements.First(element => element.Id % 3 != 0).Id;

        fixture.Explorer.SearchText = id.ToString(CultureInfo.InvariantCulture);
        fixture.Settle();

        var row = fixture.Explorer.Rows.Single(row => row.Kind == TreeRowKind.Instance && row.Id == id);
        Assert.IsTrue(row.IsDimmed);
        Assert.AreEqual(RowEye.None, row.Eye);
    }

    private static async Task<TreeRow> HiddenTypeAsync(PanelFixture fixture)
    {
        var ducts = fixture.Group(Current, "Ducts");
        await fixture.Explorer.ExpandAsync(ducts);
        var family = fixture.Explorer.Rows.First(row => row.Parent == ducts && row is { HiddenCount: > 0, Count: > 0 });
        await fixture.Explorer.ExpandAsync(family);
        var type = fixture.Explorer.Rows.First(row => row.Parent == family && row is { HiddenCount: > 0, Count: > 0 });
        await fixture.Explorer.ExpandAsync(type);
        return type;
    }
}
