using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Explorer;
using DevTools.ModelsContext.Panel;
using DevTools.ModelsContext.Scope;
using DevTools.ModelsContext.Tests.Shared;
using DevTools.ModelsContext.Visibility;

namespace DevTools.ModelsContext.Tests.Explorer;

[TestClass]
public sealed class SyncTests
{
    private static readonly DocumentKey Current = FakeModelGenerator.Current;

    [TestMethod]
    public async Task EyeKeepsRowSelection()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var (walls, host) = await SelectWallsAsync(fixture);

        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(fixture.Group(Current, "Ducts"));
        fixture.Settle();

        await AssertSelectionKeptAsync(fixture, walls, host);
        Assert.AreEqual(fixture.Group(Current, "Ducts").Count, Ids(fixture).Count);
    }

    [TestMethod]
    public async Task RowToolKeepsRowSelection()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var (walls, host) = await SelectWallsAsync(fixture);

        await fixture.Explorer.RunToolAsync(fixture.Group(Current, "Ducts"), MenuAction.Isolate);
        fixture.Settle();

        await AssertSelectionKeptAsync(fixture, walls, host);
        Assert.AreEqual(fixture.Group(Current, "Ducts").Count, Ids(fixture).Count);
    }

    [TestMethod]
    public async Task RowToolLeavesNothingSelected()
    {
        using var fixture = await PanelFixture.OpenAsync();

        await fixture.Explorer.RunToolAsync(fixture.Group(Current, "Ducts"), MenuAction.Halftone);
        fixture.Settle();

        Assert.IsEmpty(fixture.Explorer.SelectedRows);
        Assert.IsEmpty(await fixture.Model.GetSelectionAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task MenuHideActsOnSelection()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var (walls, host) = await SelectWallsAsync(fixture);

        fixture.Explorer.OpenMenu(walls);
        await fixture.Panel.ContextMenu[MenuAction.HideShow].Command.ExecuteAsync(null);
        fixture.Settle();

        await AssertSelectionKeptAsync(fixture, walls, host);
        Assert.AreEqual(walls.Count, Ids(fixture).Count);
    }

    [TestMethod]
    public async Task HotkeyActsOnSelection()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var (walls, host) = await SelectWallsAsync(fixture);

        fixture.Explorer.OpenMenu(null);
        await fixture.Panel.ContextMenu.RunShortcutAsync("H");
        fixture.Settle();

        await AssertSelectionKeptAsync(fixture, walls, host);
        Assert.AreEqual(walls.Count, Ids(fixture).Count);
    }

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
    public async Task CountShowsHostOutsideTree()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var element = fixture.Elements(Current, 1)[0];

        fixture.Model.RaiseSelectionChanged([element, new ElementRef(Current, 987654321)]);
        fixture.Settle();

        Assert.AreEqual("2 selected", fixture.Explorer.StatusText);
        Assert.IsNull(fixture.Explorer.SelectionSyncHint);
    }

    [TestMethod]
    public async Task LargeHostSelectionKeepsFullCount()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var selection = fixture.Elements(Current, 60);
        fixture.Model.RaiseSelectionChanged(selection);
        fixture.Settle();

        await fixture.Explorer.ExpandAllCommand.ExecuteAsync(null);
        fixture.Settle();

        Assert.AreEqual("60 selected", fixture.Explorer.StatusText);
        var host = await fixture.Model.GetSelectionAsync(CancellationToken.None);
        Assert.HasCount(60, host);
    }

    [TestMethod]
    public async Task SyncOffCountsTreeRows()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Model.RaiseSelectionChanged(fixture.Elements(Current, 60));
        fixture.Settle();

        fixture.Explorer.IsSelectionSyncOn = false;

        Assert.AreEqual(0, fixture.Explorer.SelectedCount);
        Assert.IsNull(fixture.Explorer.SelectionSyncHint);
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
    public async Task ShowInExplorerChecksDocuments()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Model.RaiseSelectionChanged(fixture.Elements(FakeModelGenerator.Mep, 2));
        fixture.Settle();

        Assert.IsTrue(fixture.Explorer.CanShowInExplorer);
        fixture.Explorer.ShowInExplorerCommand.Execute(null);

        CollectionAssert.Contains(fixture.Panel.ContextBar.CheckedDocuments.ToList(), FakeModelGenerator.Mep);
        Assert.HasCount(2, fixture.Explorer.SelectedRows);
    }

    private static async Task<(TreeRow Walls, HashSet<ElementRef> Host)> SelectWallsAsync(PanelFixture fixture)
    {
        var walls = fixture.Group(Current, "Walls");
        fixture.Explorer.SetSelectedRows([walls]);
        fixture.Settle();
        var host = (await fixture.Model.GetSelectionAsync(CancellationToken.None)).ToHashSet();
        Assert.HasCount(walls.Count, host);
        return (walls, host);
    }

    private static async Task AssertSelectionKeptAsync(PanelFixture fixture, TreeRow walls, HashSet<ElementRef> host)
    {
        CollectionAssert.AreEqual(new[] { walls.Key }, fixture.Explorer.SelectedRows.Select(row => row.Key).ToList());
        Assert.IsTrue(host.SetEquals(await fixture.Model.GetSelectionAsync(CancellationToken.None)));
    }

    private static IReadOnlyList<long> Ids(PanelFixture fixture)
    {
        return fixture.Actions.ExecutedPlans.Last().Steps.OfType<ElementsStep>().Single().Ids;
    }
}
