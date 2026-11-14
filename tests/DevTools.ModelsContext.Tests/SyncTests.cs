using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Tests.Shared;
using DevTools.ModelsContext.ViewModels;

namespace DevTools.ModelsContext.Tests;

[TestClass]
public sealed class SyncTests
{
    private static readonly DocumentKey Current = FakeModelGenerator.Current;

    [TestMethod]
    public async Task SyncOpensPathUnder50()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var selection = fixture.Elements(Current, 3);

        fixture.Model.RaiseSelectionChanged(selection);
        fixture.Settle();

        Assert.HasCount(3, fixture.Tree.SelectedRows);
        foreach (var element in selection)
        {
            Assert.IsTrue(fixture.Tree.Rows.Any(row => row.Kind == TreeRowKind.Instance && row.Id == element.Id));
        }
    }

    [TestMethod]
    public async Task SyncShowsBadgesAbove50()
    {
        using var fixture = await PanelFixture.OpenAsync();

        fixture.Model.RaiseSelectionChanged(fixture.Elements(Current, 60));
        fixture.Settle();

        Assert.AreEqual(TreeTabViewModel.TooManyHint, fixture.Tree.SyncHint);
        Assert.IsEmpty(fixture.Tree.SelectedRows);
        Assert.IsFalse(fixture.Tree.Rows.Any(row => row.Kind == TreeRowKind.Instance));
        Assert.AreEqual(60, fixture.Tree.Rows.Where(row => row.Kind == TreeRowKind.Group && row.Document == Current)
            .Sum(row => row.Badge));
    }

    [TestMethod]
    public async Task RowClickKeepsRowsInPlace()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var ducts = fixture.Group(Current, "Ducts");
        var resets = 0;
        var badges = 0;
        fixture.Tree.Rows.CollectionChanged += (_, _) => resets++;
        ducts.PropertyChanged += (_, e) => badges += e.PropertyName == nameof(TreeRow.Badge) ? 1 : 0;

        fixture.Tree.SetSelectedRows([ducts]);
        fixture.Settle();

        Assert.AreEqual(0, resets);
        Assert.AreEqual(1, badges);
        Assert.IsGreaterThan(0, ducts.Badge);
    }

    [TestMethod]
    public async Task SyncPausesAbove5000()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var selection = fixture.Elements(Current, 5000).Concat(fixture.Elements(FakeModelGenerator.Mep, 1)).ToList();

        fixture.Model.RaiseSelectionChanged(selection);
        fixture.Settle();
        fixture.Tree.OpenMenu(fixture.Group(Current, "Ducts"));

        Assert.IsTrue(fixture.Tree.IsSyncPaused);
        Assert.IsTrue(fixture.Panel.ContextMenu[MenuAction.SelectInHost].IsVisible);
    }

    [TestMethod]
    public async Task SelectedRowSelectsInHost()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var ducts = fixture.Group(Current, "Ducts");

        fixture.Tree.SetSelectedRows([ducts]);
        fixture.Settle();

        var selection = await fixture.Model.GetSelectionAsync(CancellationToken.None);
        Assert.AreEqual(ducts.Count, selection.Count);
        Assert.AreEqual(ducts.Count, fixture.Tree.SelectedCount);
    }

    [TestMethod]
    public async Task SelectionScopeHidesSync()
    {
        using var fixture = await PanelFixture.OpenAsync();

        fixture.Panel.ContextBar.Scope = ScopeKind.Selection;

        Assert.IsFalse(fixture.Tree.IsSyncVisible);
        Assert.IsFalse(fixture.Tree.IsSyncing);
    }

    [TestMethod]
    public async Task ShowInTreeTicksDocuments()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Model.RaiseSelectionChanged(fixture.Elements(FakeModelGenerator.Mep, 2));
        fixture.Settle();

        Assert.IsTrue(fixture.Tree.CanShowInTree);
        fixture.Tree.ShowInTreeCommand.Execute(null);

        CollectionAssert.Contains(fixture.Panel.ContextBar.TickedDocuments.ToList(), FakeModelGenerator.Mep);
        Assert.HasCount(2, fixture.Tree.SelectedRows);
    }
}
