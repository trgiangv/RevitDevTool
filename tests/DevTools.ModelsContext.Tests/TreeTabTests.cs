using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Tests.Shared;
using DevTools.ModelsContext.ViewModels;
using System.Collections.Specialized;

namespace DevTools.ModelsContext.Tests;

[TestClass]
public sealed class TreeTabTests
{
    private static readonly DocumentKey Current = FakeModelGenerator.Current;

    [TestMethod]
    public async Task TreeExpandInsertsChildren()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var ducts = fixture.Group(Current, "Ducts");

        await fixture.Tree.ExpandAsync(ducts);

        var index = fixture.Tree.Rows.IndexOf(ducts);
        Assert.IsNotEmpty(ducts.Children!);
        CollectionAssert.AreEqual(ducts.Children!.ToList(),
            fixture.Tree.Rows.Skip(index + 1).Take(ducts.Children!.Count).ToList());
    }

    [TestMethod]
    public async Task SmallExpandRaisesItemAdds()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var ducts = fixture.Group(Current, "Ducts");
        var events = new List<NotifyCollectionChangedEventArgs>();
        fixture.Tree.Rows.CollectionChanged += (_, args) => events.Add(args);

        await fixture.Tree.ExpandAsync(ducts);

        Assert.HasCount(ducts.Children!.Count, events);
        Assert.IsTrue(events.All(args => args.Action == NotifyCollectionChangedAction.Add && args.NewItems!.Count == 1));
        var index = fixture.Tree.Rows.IndexOf(ducts);
        CollectionAssert.AreEqual(Enumerable.Range(index + 1, ducts.Children!.Count).ToList(),
            events.Select(args => args.NewStartingIndex).ToList());
    }

    [TestMethod]
    public void LargeInsertRaisesOneReset()
    {
        var rows = new RangeObservableCollection<int> { 1, 2 };
        var events = new List<NotifyCollectionChangedAction>();
        rows.CollectionChanged += (_, args) => events.Add(args.Action);

        rows.InsertRange(1, Enumerable.Range(10, RangeObservableCollection<int>.ItemEventLimit + 1));

        CollectionAssert.AreEqual(new[] { NotifyCollectionChangedAction.Reset }, events);
        Assert.HasCount(RangeObservableCollection<int>.ItemEventLimit + 3, rows);
    }

    [TestMethod]
    public void SmallReplaceKeepsOrder()
    {
        var rows = new RangeObservableCollection<int> { 1, 2, 3, 4 };

        rows.ReplaceRange(1, 2, [7, 8, 9]);

        CollectionAssert.AreEqual(new[] { 1, 7, 8, 9, 4 }, rows.ToList());
    }

    [TestMethod]
    public async Task CollapseRemovesDeeperRows()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var ducts = fixture.Group(Current, "Ducts");
        var before = fixture.Tree.Rows.Count;
        await fixture.Tree.ExpandAsync(ducts);

        fixture.Tree.Collapse(ducts);

        Assert.HasCount(before, fixture.Tree.Rows);
    }

    [TestMethod]
    public async Task InstancesPageAtTwoHundred()
    {
        using var fixture = await PanelFixture.OpenAsync(Large.Value);
        var type = await LargestTypeAsync(fixture);

        await fixture.Tree.ExpandAsync(type);

        Assert.HasCount(TreeTabViewModel.InstancePageSize + 1, type.Children!);
        var more = type.Children![TreeTabViewModel.InstancePageSize];
        Assert.AreEqual(TreeRowKind.MorePage, more.Kind);
        StringAssert.StartsWith(more.Text, $"{TreeTabViewModel.InstancePageSize} of ");
    }

    [TestMethod]
    public async Task MoreLoadsNextPage()
    {
        using var fixture = await PanelFixture.OpenAsync(Large.Value);
        var type = await LargestTypeAsync(fixture);
        await fixture.Tree.ExpandAsync(type);

        await fixture.Tree.LoadMoreCommand.ExecuteAsync(type.Children![TreeTabViewModel.InstancePageSize]);

        Assert.IsGreaterThan(TreeTabViewModel.InstancePageSize + 1, type.Children!.Count);
        Assert.IsTrue(fixture.Tree.Rows.Contains(type.Children[TreeTabViewModel.InstancePageSize]));
    }

    [TestMethod]
    public async Task SearchMatchesRowText()
    {
        using var fixture = await PanelFixture.OpenAsync();

        fixture.Tree.SearchText = "duct fittings";
        fixture.Settle();

        Assert.IsTrue(fixture.Tree.Rows.Any(row => row.Kind == TreeRowKind.Group && row.Text == "Duct Fittings"));
        Assert.IsFalse(fixture.Tree.Rows.Any(row => row.Text == "Walls"));
    }

    [TestMethod]
    public async Task SearchFindsElementId()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var id = fixture.Model.Data.SourceOf(Current).Elements[1234].Id;

        fixture.Tree.SearchText = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        fixture.Settle();

        Assert.IsTrue(fixture.Tree.Rows.Any(row => row.Kind == TreeRowKind.Instance && row.Id == id));
    }

    [TestMethod]
    public async Task AllModelsGroupOnTop()
    {
        using var fixture = await PanelFixture.OpenAsync();

        var first = fixture.Tree.Rows[0];

        Assert.AreEqual(TreeRowKind.AllModelsHeader, first.Kind);
        Assert.AreEqual(TreeRowKind.AllModelsGroup, fixture.Tree.Rows[1].Kind);
    }

    [TestMethod]
    public async Task EyeHidesThenShowsRow()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var ducts = fixture.Group(Current, "Ducts");

        await fixture.Tree.ToggleEyeCommand.ExecuteAsync(ducts);
        var hidden = fixture.Group(Current, "Ducts").Eye;
        await fixture.Tree.ToggleEyeCommand.ExecuteAsync(fixture.Group(Current, "Ducts"));

        Assert.AreEqual(RowEye.Hidden, hidden);
        Assert.AreEqual(RowEye.Shown, fixture.Group(Current, "Ducts").Eye);
        Assert.AreEqual(1, fixture.Model.ResetCount);
    }

    [TestMethod]
    public async Task HiddenAllModelsRowDisablesOthers()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var row = fixture.Tree.Rows.First(row => row.Kind == TreeRowKind.AllModelsGroup && row.Text == "Ducts");

        await fixture.Tree.ToggleEyeCommand.ExecuteAsync(row);

        Assert.AreEqual(RowEye.HiddenByAllModels, fixture.Group(Current, "Ducts").Eye);
    }

    [TestMethod]
    public async Task EyeHoverHighlightsBranch()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var ducts = fixture.Group(Current, "Ducts");
        await fixture.Tree.ExpandAsync(ducts);
        var walls = fixture.Group(Current, "Walls");

        fixture.Tree.HighlightEyeTargets(ducts);
        var lit = fixture.Tree.Rows.Where(row => row.IsHighlighted).ToList();
        fixture.Tree.ClearHighlight();

        CollectionAssert.AreEquivalent(ducts.Children!.Prepend(ducts).ToList(), lit);
        Assert.IsFalse(walls.IsHighlighted);
        Assert.IsFalse(fixture.Tree.Rows.Any(row => row.IsHighlighted));
    }

    [TestMethod]
    public async Task AllModelsHoverLightsGroups()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var row = fixture.Tree.Rows.First(row => row.Kind == TreeRowKind.AllModelsGroup && row.Text == "Ducts");

        fixture.Tree.HighlightEyeTargets(row);

        Assert.IsTrue(row.IsHighlighted);
        Assert.IsTrue(fixture.Group(Current, "Ducts").IsHighlighted);
        Assert.IsFalse(fixture.Group(Current, "Walls").IsHighlighted);
    }

    [TestMethod]
    public async Task ColorToolAsksThenApplies()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var requested = 0;
        fixture.Panel.ContextMenu.ColorRequested += (_, _) => requested++;

        await fixture.Tree.RunToolAsync(fixture.Group(Current, "Ducts"), MenuAction.Color);
        await fixture.Panel.ContextMenu.ApplyColorCommand.ExecuteAsync(new RgbColor(255, 0, 0));

        var plan = fixture.Model.ExecutedPlans.Single();
        Assert.AreEqual(1, requested);
        Assert.AreEqual(VisibilityAction.Color, plan.Action);
        Assert.AreEqual(new RgbColor(255, 0, 0), plan.Color);
    }

    [TestMethod]
    public async Task ToolActsOnWholeSelection()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var ducts = fixture.Group(Current, "Ducts");
        var walls = fixture.Group(Current, "Walls");
        fixture.Tree.SetSelectedRows([ducts, walls]);

        await fixture.Tree.RunToolAsync(ducts, MenuAction.Isolate);

        var plan = fixture.Model.ExecutedPlans.Single();
        Assert.AreEqual(VisibilityAction.Isolate, plan.Action);
        CollectionAssert.AreEquivalent(new[] { ducts, walls }, fixture.Tree.SelectedRows.ToList());
    }

    [TestMethod]
    public async Task ToolOutsideSelectionSelectsRow()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var ducts = fixture.Group(Current, "Ducts");
        fixture.Tree.SetSelectedRows([fixture.Group(Current, "Walls")]);

        await fixture.Tree.RunToolAsync(ducts, MenuAction.Isolate);

        CollectionAssert.AreEqual(new[] { ducts }, fixture.Tree.SelectedRows.ToList());
        Assert.HasCount(1, fixture.Model.ExecutedPlans);
    }

    [TestMethod]
    public async Task ToolAndMenuShareReason()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Panel.ContextBar.Tick([FakeModelGenerator.Mep]);
        var linked = fixture.Group(FakeModelGenerator.Mep, "Ducts");

        var tool = fixture.Tree.ReasonFor(linked, MenuAction.Halftone);
        fixture.Tree.OpenMenu(linked);

        Assert.IsNotNull(tool);
        Assert.AreEqual(fixture.Panel.ContextMenu[MenuAction.Halftone].DisabledReason, tool);
        Assert.AreEqual(fixture.Panel.ContextMenu[MenuAction.Isolate].DisabledReason,
            fixture.Tree.ReasonFor(linked, MenuAction.Isolate));
    }

    [TestMethod]
    public async Task DisabledToolReportsReason()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Panel.ContextBar.Tick([FakeModelGenerator.Mep]);

        await fixture.Tree.RunToolAsync(fixture.Group(FakeModelGenerator.Mep, "Ducts"), MenuAction.Halftone);

        Assert.AreEqual(VisibilityReasons.LinkChildOnlyShowIsolate, fixture.Tree.Message);
        Assert.IsEmpty(fixture.Model.ExecutedPlans);
    }

    [TestMethod]
    public async Task InstanceRowHasNoCount()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var ducts = fixture.Group(Current, "Ducts");
        await fixture.Tree.ExpandAsync(ducts);
        while (!fixture.Tree.Rows.Any(row => row.Kind == TreeRowKind.Instance))
        {
            await fixture.Tree.ExpandAsync(fixture.Tree.Rows.First(row =>
                row.HasChildren && !row.IsExpanded && row.Level > ducts.Level));
        }

        var instance = fixture.Tree.Rows.First(row => row.Kind == TreeRowKind.Instance);

        Assert.IsFalse(instance.HasCount);
        Assert.IsTrue(ducts.HasCount);
    }
    [TestMethod]
    public async Task MenuColorAsksThenApplies()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var menu = fixture.Panel.ContextMenu;
        var requested = 0;
        menu.ColorRequested += (_, _) => requested++;
        fixture.Tree.OpenMenu(fixture.Group(Current, "Ducts"));

        await menu[MenuAction.Color].Command.ExecuteAsync(null);
        await menu.ApplyColorCommand.ExecuteAsync(new RgbColor(0, 0, 255));

        Assert.AreEqual(1, requested);
        Assert.AreEqual(new RgbColor(0, 0, 255), fixture.Model.ExecutedPlans.Single().Color);
    }

    [TestMethod]
    public async Task RowsCarryIndentAndCount()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var ducts = fixture.Group(Current, "Ducts");

        Assert.AreEqual(ducts.Level * TreeRow.IndentPerLevel, ducts.Indent);
        Assert.AreEqual(ducts.Count.ToString("N0", System.Globalization.CultureInfo.CurrentCulture), ducts.CountText);
    }

    private static readonly Lazy<FakeModelData> Large = new(() => FakeModelGenerator.Generate(new FakeModelOptions
    {
        CurrentElements = 20_000,
        LinkElements = 100
    }));

    private static async Task<TreeRow> LargestTypeAsync(PanelFixture fixture)
    {
        var snapshot = FakeModelGenerator.BuildSnapshot(fixture.Model.Data.SourceOf(Current));
        var (group, family, type) = snapshot.Counts.Children.Values
            .SelectMany(groupNode => groupNode.Children.Values.SelectMany(familyNode => familyNode.Children.Values
                .Select(typeNode => (groupNode, familyNode, typeNode))))
            .OrderByDescending(path => path.typeNode.Count)
            .First();
        Assert.IsGreaterThan(TreeTabViewModel.InstancePageSize, type.Count);

        var groupRow = fixture.Group(Current, snapshot.GroupName(group.Key));
        await fixture.Tree.ExpandAsync(groupRow);
        var familyRow = groupRow.Children!.First(row => row.Text == snapshot.Names[family.Key]);
        await fixture.Tree.ExpandAsync(familyRow);
        return familyRow.Children!.First(row => row.Text == snapshot.TypeName(type.Key));
    }
}
