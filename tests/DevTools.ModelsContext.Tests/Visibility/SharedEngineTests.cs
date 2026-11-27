using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Explorer;
using DevTools.ModelsContext.Panel;
using DevTools.ModelsContext.Scope;
using DevTools.ModelsContext.Tests.Shared;
using DevTools.ModelsContext.Visibility;

namespace DevTools.ModelsContext.Tests.Visibility;

[TestClass]
public sealed class SharedEngineTests
{
    private static readonly DocumentKey Current = FakeModelGenerator.Current;
    private static readonly DocumentKey Mep = FakeModelGenerator.Mep;

    [TestMethod]
    public async Task BranchHideMatchesSelect()
    {
        using var fixture = await PanelFixture.OpenAsync(scope: ScopeKind.ActiveView);
        var ducts = fixture.Group(Current, "Ducts");
        var selected = fixture.Explorer.ElementsOf([ducts]).Select(element => element.Id).ToList();
        var whole = fixture.Model.SnapshotOf(Current).IdsUnder(ducts.Path).Count();

        await fixture.Explorer.ToggleHiddenCommand.ExecuteAsync(ducts);

        var step = (ElementsStep)fixture.Actions.ExecutedPlans.Single().Steps.Single();
        CollectionAssert.AreEquivalent(selected, step.Ids.ToList());
        Assert.AreEqual(ducts.Count, step.Ids.Count);
        Assert.IsLessThan(whole, step.Ids.Count);
    }

    [TestMethod]
    public async Task ScopedHideShowsHiddenEye()
    {
        using var fixture = await PanelFixture.OpenAsync(scope: ScopeKind.ActiveView);

        await fixture.Explorer.ToggleHiddenCommand.ExecuteAsync(fixture.Group(Current, "Ducts"));

        Assert.AreEqual(HideState.Hidden, fixture.Group(Current, "Ducts").HideState);
    }

    [TestMethod]
    public async Task ScopedShowRestoresBranch()
    {
        using var fixture = await PanelFixture.OpenAsync(scope: ScopeKind.ActiveView);
        await fixture.Explorer.ToggleHiddenCommand.ExecuteAsync(fixture.Group(Current, "Ducts"));

        await fixture.Explorer.ToggleHiddenCommand.ExecuteAsync(fixture.Group(Current, "Ducts"));

        Assert.AreEqual(HideState.Shown, fixture.Group(Current, "Ducts").HideState);
    }

    [TestMethod]
    public async Task FilterResultReadsHidden()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var ducts = fixture.Explorer.ElementsOf([fixture.Group(Current, "Ducts")]).Select(element => element.Id).ToList();
        fixture.Explorer.CollapseTree();
        await fixture.Actions.ExecuteAsync(new VisibilityPlan(VisibilityAction.Hide, null,
            [new ElementsStep(Current, ducts)], null), CancellationToken.None);
        fixture.Model.RaiseViewActivated(new ViewInfo(Current, "view-2", "Level 2", ViewKind.Plan));
        fixture.Settle();
        CheckDucts(fixture);

        fixture.Filter.OpenResultMenu();

        Assert.AreEqual("Show", fixture.Panel.ContextMenu[MenuAction.HideShow].Text);
    }

    [TestMethod]
    public async Task FilterColorMarksExplorerRow()
    {
        using var fixture = await PanelFixture.OpenAsync();
        CheckDucts(fixture);
        var menu = fixture.Panel.ContextMenu;
        fixture.Filter.OpenResultMenu();

        await menu.ApplyColorCommand.ExecuteAsync(new RgbColor(10, 20, 30));

        Assert.AreEqual(new RgbColor(10, 20, 30), fixture.Group(Current, "Ducts").OverrideColor);
        Assert.IsFalse(fixture.Group(Current, "Walls").HasColor);
    }

    [TestMethod]
    public async Task OneInstanceLeavesGroupPlain()
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

        await fixture.Explorer.RunToolAsync(instance, MenuAction.Halftone);

        Assert.IsTrue(instance.IsHalftone);
        Assert.IsFalse(ducts.IsHalftone);
    }

    [TestMethod]
    public async Task LinkRowHalftoneMarksRow()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Panel.ContextBar.Check([Mep]);
        var link = fixture.Explorer.Rows.First(row => row.Kind == TreeRowKind.LinkModel && row.Document == Mep);

        await fixture.Explorer.RunToolAsync(link, MenuAction.Halftone);

        Assert.IsTrue(link.IsHalftone);
        Assert.IsTrue(fixture.Group(Mep, "Ducts").IsHalftone);
    }

    private static void CheckDucts(PanelFixture fixture)
    {
        var filter = fixture.Filter;
        filter.Library.AddCommand.Execute(null);
        filter.Categories.Items.First(category => category.Name == "Ducts").IsChecked = true;
        fixture.Settle();
    }
}
