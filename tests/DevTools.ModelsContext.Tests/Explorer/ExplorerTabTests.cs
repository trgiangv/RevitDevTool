using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Explorer;
using DevTools.ModelsContext.Panel;
using DevTools.ModelsContext.Tests.Shared;
using DevTools.ModelsContext.Visibility;
using System.Collections.Specialized;

namespace DevTools.ModelsContext.Tests.Explorer;

[TestClass]
public sealed class ExplorerTabTests
{
    private static readonly DocumentKey Current = FakeModelGenerator.Current;

    [TestMethod]
    public async Task ExpandInsertsChildren()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var ducts = fixture.Group(Current, "Ducts");

        await fixture.Explorer.ExpandAsync(ducts);

        var index = fixture.Explorer.Rows.IndexOf(ducts);
        Assert.IsNotEmpty(ducts.Children!);
        CollectionAssert.AreEqual(ducts.Children!.ToList(),
            fixture.Explorer.Rows.Skip(index + 1).Take(ducts.Children!.Count).ToList());
    }

    [TestMethod]
    public async Task SmallExpandRaisesItemAdds()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var ducts = fixture.Group(Current, "Ducts");
        var events = new List<NotifyCollectionChangedEventArgs>();
        fixture.Explorer.Rows.CollectionChanged += (_, args) => events.Add(args);

        await fixture.Explorer.ExpandAsync(ducts);

        Assert.HasCount(ducts.Children!.Count, events);
        Assert.IsTrue(events.All(args => args.Action == NotifyCollectionChangedAction.Add && args.NewItems!.Count == 1));
        var index = fixture.Explorer.Rows.IndexOf(ducts);
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
    public async Task ExpandAllStopsAtTypes()
    {
        using var fixture = await PanelFixture.OpenAsync();

        await fixture.Explorer.ExpandTreeAsync();

        var rows = fixture.Explorer.Rows;
        Assert.IsTrue(rows.Any(row => row.Kind == TreeRowKind.Type));
        Assert.IsFalse(rows.Any(row => row.Kind == TreeRowKind.Instance));
        Assert.IsTrue(rows.Where(row => row.HasChildren && row.Kind != TreeRowKind.Type).All(row => row.IsExpanded));
    }

    [TestMethod]
    public async Task ExpandAllRebuildsRowsOnce()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var events = new List<NotifyCollectionChangedAction>();
        fixture.Explorer.Rows.CollectionChanged += (_, args) => events.Add(args.Action);

        await fixture.Explorer.ExpandTreeAsync();

        CollectionAssert.AreEqual(new[] { NotifyCollectionChangedAction.Reset }, events);
    }

    [TestMethod]
    public async Task CollapseAllLeavesRoots()
    {
        using var fixture = await PanelFixture.OpenAsync();
        await fixture.Explorer.ExpandTreeAsync();

        fixture.Explorer.CollapseTree();

        Assert.IsTrue(fixture.Explorer.Rows.All(row => row.Level == 0 && !row.IsExpanded));
    }

    [TestMethod]
    public async Task ExpandAllSurvivesRefresh()
    {
        using var fixture = await PanelFixture.OpenAsync();
        await fixture.Explorer.ExpandTreeAsync();
        var expanded = fixture.Explorer.Rows.Count;

        await fixture.Panel.RefreshAsync();

        Assert.HasCount(expanded, fixture.Explorer.Rows);
    }

    [TestMethod]
    public async Task CollapseAllSurvivesRefresh()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Explorer.CollapseTree();

        await fixture.Panel.RefreshAsync();

        Assert.IsTrue(fixture.Explorer.Rows.All(row => row.Level == 0));
    }

    [TestMethod]
    public async Task CollapseRemovesDeeperRows()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var ducts = fixture.Group(Current, "Ducts");
        var before = fixture.Explorer.Rows.Count;
        await fixture.Explorer.ExpandAsync(ducts);

        fixture.Explorer.Collapse(ducts);

        Assert.HasCount(before, fixture.Explorer.Rows);
    }

    [TestMethod]
    public async Task InstancesPageAtTwoHundred()
    {
        using var fixture = await PanelFixture.OpenAsync(Large.Value);
        var type = await LargestTypeAsync(fixture);

        await fixture.Explorer.ExpandAsync(type);

        Assert.HasCount(ExplorerTabViewModel.InstancePageSize + 1, type.Children!);
        var more = type.Children![ExplorerTabViewModel.InstancePageSize];
        Assert.AreEqual(TreeRowKind.MorePage, more.Kind);
        StringAssert.StartsWith(more.Text, $"{ExplorerTabViewModel.InstancePageSize} of ");
    }

    [TestMethod]
    public async Task MoreLoadsNextPage()
    {
        using var fixture = await PanelFixture.OpenAsync(Large.Value);
        var type = await LargestTypeAsync(fixture);
        await fixture.Explorer.ExpandAsync(type);

        await fixture.Explorer.LoadMoreCommand.ExecuteAsync(type.Children![ExplorerTabViewModel.InstancePageSize]);

        Assert.IsGreaterThan(ExplorerTabViewModel.InstancePageSize + 1, type.Children!.Count);
        Assert.IsTrue(fixture.Explorer.Rows.Contains(type.Children[ExplorerTabViewModel.InstancePageSize]));
    }

    [TestMethod]
    public async Task SearchMatchesRowText()
    {
        using var fixture = await PanelFixture.OpenAsync();

        fixture.Explorer.SearchText = "duct fittings";
        fixture.Settle();

        Assert.IsTrue(fixture.Explorer.Rows.Any(row => row.Kind == TreeRowKind.Group && row.Text == "Duct Fittings"));
        Assert.IsFalse(fixture.Explorer.Rows.Any(row => row.Text == "Walls"));
    }

    [TestMethod]
    public async Task SearchFindsElementId()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var id = fixture.Model.Data.SourceOf(Current).Elements[1234].Id;

        fixture.Explorer.SearchText = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        fixture.Settle();

        Assert.IsTrue(fixture.Explorer.Rows.Any(row => row.Kind == TreeRowKind.Instance && row.Id == id));
    }

    [TestMethod]
    public async Task TreeStartsAtModelRow()
    {
        using var fixture = await PanelFixture.OpenAsync();

        Assert.AreEqual(TreeRowKind.CurrentModelHeader, fixture.Explorer.Rows[0].Kind);
        Assert.IsFalse(fixture.Explorer.Rows.Any(row => row.Kind == TreeRowKind.Category));
    }

    [TestMethod]
    public async Task EyeHidesThenShowsRow()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var ducts = fixture.Group(Current, "Ducts");

        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(ducts);
        var hidden = fixture.Group(Current, "Ducts").Eye;
        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(fixture.Group(Current, "Ducts"));

        Assert.AreEqual(RowEye.Hidden, hidden);
        Assert.AreEqual(RowEye.Shown, fixture.Group(Current, "Ducts").Eye);
        Assert.AreEqual(1, fixture.Actions.ResetCount);
    }

    [TestMethod]
    public async Task CategoryEyeMarksGroupRow()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var ducts = fixture.Explorer.Categories.Picker.Items.First(item => item.Name == "Ducts");

        await ducts.ToggleEyeCommand.ExecuteAsync(null);

        Assert.IsTrue(ducts.IsHiddenInView);
        Assert.AreEqual(RowEye.HiddenByCategory, fixture.Group(Current, "Ducts").Eye);
    }

    [TestMethod]
    public async Task EyeHoverHighlightsBranch()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var ducts = fixture.Group(Current, "Ducts");
        await fixture.Explorer.ExpandAsync(ducts);
        var walls = fixture.Group(Current, "Walls");

        fixture.Explorer.HighlightEyeTargets(ducts);
        var lit = fixture.Explorer.Rows.Where(row => row.IsHighlighted).ToList();
        fixture.Explorer.ClearHighlight();

        CollectionAssert.AreEquivalent(ducts.Children!.Prepend(ducts).ToList(), lit);
        Assert.IsFalse(walls.IsHighlighted);
        Assert.IsFalse(fixture.Explorer.Rows.Any(row => row.IsHighlighted));
    }


    [TestMethod]
    public async Task ColorToolAsksThenApplies()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var requested = 0;
        fixture.Panel.ContextMenu.ColorRequested += (_, _) => requested++;

        await fixture.Explorer.RunToolAsync(fixture.Group(Current, "Ducts"), MenuAction.Color);
        await fixture.Panel.ContextMenu.ApplyColorCommand.ExecuteAsync(new RgbColor(255, 0, 0));

        var plan = fixture.Actions.ExecutedPlans.Single();
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
        fixture.Explorer.SetSelectedRows([ducts, walls]);

        await fixture.Explorer.RunToolAsync(ducts, MenuAction.Isolate);

        var plan = fixture.Actions.ExecutedPlans.Single();
        Assert.AreEqual(VisibilityAction.Isolate, plan.Action);
        CollectionAssert.AreEquivalent(new[] { ducts, walls }, fixture.Explorer.SelectedRows.ToList());
    }

    [TestMethod]
    public async Task ToolOutsideSelectionSelectsRow()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var ducts = fixture.Group(Current, "Ducts");
        fixture.Explorer.SetSelectedRows([fixture.Group(Current, "Walls")]);

        await fixture.Explorer.RunToolAsync(ducts, MenuAction.Isolate);

        CollectionAssert.AreEqual(new[] { ducts }, fixture.Explorer.SelectedRows.ToList());
        Assert.HasCount(1, fixture.Actions.ExecutedPlans);
    }

    [TestMethod]
    public async Task ToolAndMenuShareReason()
    {
        using var fixture = await PanelFixture.OpenAsync(TestModel.WithoutLinkFilters());
        fixture.Panel.ContextBar.Check([FakeModelGenerator.Mep]);
        var linked = fixture.Group(FakeModelGenerator.Mep, "Ducts");

        var tool = fixture.Explorer.ReasonFor(linked, MenuAction.Halftone);
        fixture.Explorer.OpenMenu(linked);

        Assert.IsNotNull(tool);
        Assert.AreEqual(fixture.Panel.ContextMenu[MenuAction.Halftone].DisabledReason, tool);
        Assert.AreEqual(fixture.Panel.ContextMenu[MenuAction.Isolate].DisabledReason,
            fixture.Explorer.ReasonFor(linked, MenuAction.Isolate));
    }

    [TestMethod]
    public async Task DisabledToolReportsReason()
    {
        using var fixture = await PanelFixture.OpenAsync(TestModel.WithoutLinkFilters());
        fixture.Panel.ContextBar.Check([FakeModelGenerator.Mep]);

        await fixture.Explorer.RunToolAsync(fixture.Group(FakeModelGenerator.Mep, "Ducts"), MenuAction.Halftone);

        Assert.AreEqual(VisibilityText.LinkFiltersUnavailable, fixture.Panel.Message);
        Assert.IsEmpty(fixture.Actions.ExecutedPlans);
    }

    [TestMethod]
    public async Task InstanceRowHasNoCount()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var ducts = fixture.Group(Current, "Ducts");
        await fixture.Explorer.ExpandAsync(ducts);
        while (!fixture.Explorer.Rows.Any(row => row.Kind == TreeRowKind.Instance))
        {
            await fixture.Explorer.ExpandAsync(fixture.Explorer.Rows.First(row =>
                row.HasChildren && !row.IsExpanded && row.Level > ducts.Level));
        }

        var instance = fixture.Explorer.Rows.First(row => row.Kind == TreeRowKind.Instance);

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
        fixture.Explorer.OpenMenu(fixture.Group(Current, "Ducts"));

        await menu[MenuAction.Color].Command.ExecuteAsync(null);
        await menu.ApplyColorCommand.ExecuteAsync(new RgbColor(0, 0, 255));

        Assert.AreEqual(1, requested);
        Assert.AreEqual(new RgbColor(0, 0, 255), fixture.Actions.ExecutedPlans.Single().Color);
    }

    [TestMethod]
    public async Task ColorShowsOnRow()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var ducts = fixture.Group(Current, "Ducts");

        await fixture.Explorer.RunToolAsync(ducts, MenuAction.Color);
        await fixture.Panel.ContextMenu.ApplyColorCommand.ExecuteAsync(new RgbColor(255, 0, 16));

        Assert.AreEqual(new RgbColor(255, 0, 16), ducts.OverrideColor);
        Assert.AreEqual("#FF0010", ducts.ColorHex);
        Assert.IsFalse(fixture.Group(Current, "Walls").HasColor);
    }

    [TestMethod]
    public async Task ChildInheritsColor()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var ducts = fixture.Group(Current, "Ducts");
        await fixture.Explorer.ExpandAsync(ducts);

        await fixture.Explorer.RunToolAsync(ducts, MenuAction.Color);
        await fixture.Panel.ContextMenu.ApplyColorCommand.ExecuteAsync(new RgbColor(0, 128, 0));

        Assert.IsNotEmpty(ducts.Children!);
        Assert.IsTrue(ducts.Children!.All(child => child.OverrideColor == new RgbColor(0, 128, 0)));
    }

    [TestMethod]
    public async Task CategoryColorReachesGroups()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var picker = fixture.Explorer.Categories.Picker;
        picker.SetChecks([], picker.Items.Where(item => item.Name != "Ducts").ToList());

        await fixture.Explorer.Categories.ColorCommand.ExecuteAsync(null);
        await fixture.Panel.ContextMenu.ApplyColorCommand.ExecuteAsync(new RgbColor(1, 2, 3));

        Assert.IsInstanceOfType<CategoryStep>(fixture.Actions.ExecutedPlans.Single().Steps.Single());
        Assert.AreEqual(new RgbColor(1, 2, 3), fixture.Group(Current, "Ducts").OverrideColor);
    }

    [TestMethod]
    public async Task ResetClearsColor()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var ducts = fixture.Group(Current, "Ducts");
        await fixture.Explorer.RunToolAsync(ducts, MenuAction.Color);
        await fixture.Panel.ContextMenu.ApplyColorCommand.ExecuteAsync(new RgbColor(255, 0, 0));

        await fixture.Panel.TemporaryBar.ResetCommand.ExecuteAsync(null);

        Assert.IsNull(fixture.Group(Current, "Ducts").OverrideColor);
    }

    [TestMethod]
    public async Task KeepLeavesColorRecorded()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var ducts = fixture.Group(Current, "Ducts");
        await fixture.Explorer.RunToolAsync(ducts, MenuAction.Halftone);

        await fixture.Panel.TemporaryBar.KeepCommand.ExecuteAsync(null);

        Assert.IsTrue(fixture.Group(Current, "Ducts").IsHalftone);
        Assert.AreEqual(1, fixture.Panel.TemporaryBar.ChangeCount);
    }

    [TestMethod]
    public async Task HalftoneMarksRow()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var ducts = fixture.Group(Current, "Ducts");

        await fixture.Explorer.RunToolAsync(ducts, MenuAction.Halftone);
        await fixture.Explorer.RunToolAsync(ducts, MenuAction.Color);
        await fixture.Panel.ContextMenu.ApplyColorCommand.ExecuteAsync(new RgbColor(9, 9, 9));

        Assert.IsTrue(ducts.IsHalftone);
        Assert.AreEqual(new RgbColor(9, 9, 9), ducts.OverrideColor);
        Assert.IsFalse(fixture.Group(Current, "Walls").IsHalftone);
    }

    [TestMethod]
    public async Task ShowKeepsColor()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var ducts = fixture.Group(Current, "Ducts");
        await fixture.Explorer.RunToolAsync(ducts, MenuAction.Color);
        await fixture.Panel.ContextMenu.ApplyColorCommand.ExecuteAsync(new RgbColor(255, 0, 0));

        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(fixture.Group(Current, "Walls"));
        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(fixture.Group(Current, "Walls"));

        Assert.AreEqual(new RgbColor(255, 0, 0), fixture.Group(Current, "Ducts").OverrideColor);
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
        Assert.IsGreaterThan(ExplorerTabViewModel.InstancePageSize, type.Count);

        var groupRow = fixture.Group(Current, snapshot.GroupName(group.Key));
        await fixture.Explorer.ExpandAsync(groupRow);
        var familyRow = groupRow.Children!.First(row => row.Text == snapshot.Names[family.Key]);
        await fixture.Explorer.ExpandAsync(familyRow);
        return familyRow.Children!.First(row => row.Text == snapshot.TypeName(type.Key));
    }
}
