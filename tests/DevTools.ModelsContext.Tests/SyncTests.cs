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

        Assert.HasCount(3, fixture.Explorer.SelectedRows);
        foreach (var element in selection)
        {
            Assert.IsTrue(fixture.Explorer.Rows.Any(row => row.Kind == TreeRowKind.Instance && row.Id == element.Id));
        }
    }

    [TestMethod]
    public async Task SyncShowsBadgesAbove50()
    {
        using var fixture = await PanelFixture.OpenAsync();

        fixture.Model.RaiseSelectionChanged(fixture.Elements(Current, 60));
        fixture.Settle();

        Assert.AreEqual(ExplorerTabViewModel.TooManyHint, fixture.Explorer.SelectionSyncHint);
        Assert.IsEmpty(fixture.Explorer.SelectedRows);
        Assert.IsFalse(fixture.Explorer.Rows.Any(row => row.Kind == TreeRowKind.Instance));
        Assert.AreEqual(60, fixture.Explorer.Rows.Where(row => row.Kind == TreeRowKind.Group && row.Document == Current)
            .Sum(row => row.Badge));
    }

    [TestMethod]
    public async Task RowClickKeepsRowsInPlace()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var ducts = fixture.Group(Current, "Ducts");
        var resets = 0;
        var badges = 0;
        fixture.Explorer.Rows.CollectionChanged += (_, _) => resets++;
        ducts.PropertyChanged += (_, e) => badges += e.PropertyName == nameof(TreeRow.Badge) ? 1 : 0;

        fixture.Explorer.SetSelectedRows([ducts]);
        fixture.Settle();

        Assert.AreEqual(0, resets);
        Assert.AreEqual(1, badges);
        Assert.IsGreaterThan(0, ducts.Badge);
    }

    [TestMethod]
    public async Task NextClickHidesOldBadge()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var ducts = fixture.Group(Current, "Ducts");
        var pipes = fixture.Group(Current, "Pipes");
        fixture.Explorer.SetSelectedRows([ducts]);
        fixture.Settle();
        var shown = new List<int>();
        ducts.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TreeRow.Badge))
            {
                shown.Add(ducts.Badge);
            }
        };

        fixture.Explorer.SetSelectedRows([pipes]);
        Assert.AreEqual(0, ducts.Badge);
        fixture.Settle();

        Assert.AreEqual(0, ducts.Badge);
        CollectionAssert.AreEqual(new[] { 0 }, shown);
    }

    [TestMethod]
    public async Task LateEchoKeepsNewClick()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Model.EchoSelection = false;
        var ducts = fixture.Group(Current, "Ducts");
        var pipes = fixture.Group(Current, "Pipes");
        fixture.Explorer.SetSelectedRows([ducts]);
        fixture.Settle();
        var first = await fixture.Model.GetSelectionAsync(CancellationToken.None);
        fixture.Explorer.SetSelectedRows([pipes]);
        fixture.Settle();

        fixture.Model.RaiseSelectionChanged(first);
        fixture.Settle();

        CollectionAssert.AreEqual(new[] { pipes }, fixture.Explorer.SelectedRows.ToList());
        Assert.AreEqual(pipes.Count, fixture.Explorer.SelectedCount);
    }

    [TestMethod]
    public async Task ClickWinsOverHostEvent()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var ducts = fixture.Group(Current, "Ducts");

        fixture.Model.RaiseSelectionChanged(fixture.Elements(Current, 2));
        fixture.Explorer.SetSelectedRows([ducts]);
        fixture.Settle();

        CollectionAssert.AreEqual(new[] { ducts }, fixture.Explorer.SelectedRows.ToList());
        var selection = await fixture.Model.GetSelectionAsync(CancellationToken.None);
        Assert.AreEqual(ducts.Count, selection.Count);
    }

    [TestMethod]
    public async Task CountShowsBeforeSend()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var ducts = fixture.Group(Current, "Ducts");

        fixture.Explorer.SetSelectedRows([ducts]);

        Assert.AreEqual(ducts.Count, fixture.Explorer.SelectedCount);
    }

    [TestMethod]
    public async Task CountFollowsTreeRows()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Model.EchoSelection = false;
        var element = fixture.Elements(Current, 1)[0];
        fixture.Model.RaiseSelectionChanged([element]);
        fixture.Settle();
        var row = fixture.Explorer.SelectedRows.Single();
        fixture.Explorer.SetSelectedRows([row]);
        fixture.Settle();

        fixture.Model.RaiseSelectionChanged([element, new ElementRef(Current, 987654321)]);
        fixture.Settle();

        Assert.AreEqual(1, fixture.Explorer.SelectedCount);
        Assert.AreEqual("1 selected", fixture.Explorer.StatusText);
    }

    [TestMethod]
    public async Task SyncPausesAbove5000()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var selection = fixture.Elements(Current, 5000).Concat(fixture.Elements(FakeModelGenerator.Mep, 1)).ToList();

        fixture.Model.RaiseSelectionChanged(selection);
        fixture.Settle();
        fixture.Explorer.OpenMenu(fixture.Group(Current, "Ducts"));

        Assert.IsTrue(fixture.Explorer.IsSelectionSyncPaused);
        Assert.IsTrue(fixture.Panel.ContextMenu[MenuAction.SelectInHost].IsVisible);
    }

    [TestMethod]
    public async Task SelectedRowSelectsInHost()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var ducts = fixture.Group(Current, "Ducts");

        fixture.Explorer.SetSelectedRows([ducts]);
        fixture.Settle();

        var selection = await fixture.Model.GetSelectionAsync(CancellationToken.None);
        Assert.AreEqual(ducts.Count, selection.Count);
        Assert.AreEqual(ducts.Count, fixture.Explorer.SelectedCount);
    }

    [TestMethod]
    public async Task SelectionScopeHidesSync()
    {
        using var fixture = await PanelFixture.OpenAsync();

        fixture.Panel.ContextBar.Scope = ScopeKind.Selection;

        Assert.IsFalse(fixture.Explorer.IsSelectionSyncVisible);
        Assert.IsFalse(fixture.Explorer.IsSelectionSyncing);
    }

    [TestMethod]
    public async Task ShowInExplorerTicksDocuments()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Model.RaiseSelectionChanged(fixture.Elements(FakeModelGenerator.Mep, 2));
        fixture.Settle();

        Assert.IsTrue(fixture.Explorer.CanShowInExplorer);
        fixture.Explorer.ShowInExplorerCommand.Execute(null);

        CollectionAssert.Contains(fixture.Panel.ContextBar.TickedDocuments.ToList(), FakeModelGenerator.Mep);
        Assert.HasCount(2, fixture.Explorer.SelectedRows);
    }
}
