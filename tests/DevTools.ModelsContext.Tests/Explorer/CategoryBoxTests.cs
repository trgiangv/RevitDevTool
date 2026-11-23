using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Explorer;
using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Scope;
using DevTools.ModelsContext.Tests.Shared;
using DevTools.ModelsContext.Visibility;

namespace DevTools.ModelsContext.Tests.Explorer;

[TestClass]
public sealed class CategoryBoxTests
{
    private static readonly DocumentKey Current = FakeModelGenerator.Current;

    [TestMethod]
    public async Task CategoriesStartTicked()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var picker = fixture.Explorer.Categories.Picker;

        Assert.IsNotEmpty(picker.Items);
        Assert.IsTrue(picker.Items.All(item => item.IsChecked));
        Assert.AreEqual(CategoryPickerViewModel.AllCategories, picker.Summary);
    }

    [TestMethod]
    public async Task UntickDropsGroupRow()
    {
        using var fixture = await PanelFixture.OpenAsync();

        Item(fixture, "Ducts").IsChecked = false;

        Assert.IsNull(fixture.FindGroup(Current, "Ducts"));
        Assert.IsNotNull(fixture.FindGroup(Current, "Walls"));
    }

    [TestMethod]
    public async Task ModelCountFollowsTicks()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var before = fixture.Explorer.Rows[0].Count;
        var ducts = fixture.Group(Current, "Ducts").Count;

        Item(fixture, "Ducts").IsChecked = false;

        Assert.AreEqual(before - ducts, fixture.Explorer.Rows[0].Count);
    }

    [TestMethod]
    public async Task NewCategoryShowsTicked()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var bar = fixture.Panel.ContextBar;
        bar.Tick([FakeModelGenerator.ArchFirst]);
        bar.Documents.First(item => item.Key == Current).IsChecked = false;
        Assert.IsFalse(fixture.Explorer.Categories.Picker.Items.Any(item => item.Name == "Ducts"));
        Item(fixture, "Walls").IsChecked = false;

        bar.Tick([Current]);

        Assert.IsTrue(Item(fixture, "Ducts").IsChecked);
        Assert.IsFalse(Item(fixture, "Walls").IsChecked);
    }

    [TestMethod]
    public async Task IsolateActsOnTicked()
    {
        using var fixture = await PanelFixture.OpenAsync();
        TickOnly(fixture, "Ducts", "Walls");

        await fixture.Explorer.Categories.IsolateCommand.ExecuteAsync(null);

        var plan = fixture.Actions.ExecutedPlans.Single();
        Assert.AreEqual(VisibilityAction.Isolate, plan.Action);
        CollectionAssert.AreEquivalent(new[] { TestModel.Ducts, TestModel.Walls },
            plan.Steps.Cast<GroupEverywhereStep>().Select(step => step.GroupId).ToList());
    }

    [TestMethod]
    public async Task HalftoneActsOnTicked()
    {
        using var fixture = await PanelFixture.OpenAsync();
        TickOnly(fixture, "Ducts");

        await fixture.Explorer.Categories.HalftoneCommand.ExecuteAsync(null);

        var step = (GroupEverywhereStep)fixture.Actions.ExecutedPlans.Single().Steps.Single();
        Assert.AreEqual(TestModel.Ducts, step.GroupId);
    }

    [TestMethod]
    public async Task NoTickDisablesMenu()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var categories = fixture.Explorer.Categories;

        TickOnly(fixture);

        Assert.IsFalse(categories.IsolateCommand.CanExecute(null));
        Assert.IsFalse(categories.HalftoneCommand.CanExecute(null));
        Assert.IsFalse(categories.ColorCommand.CanExecute(null));
        Assert.AreEqual(ExplorerCategories.SelectReason, categories.MenuReason);
    }

    [TestMethod]
    public async Task TickEnablesMenu()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var categories = fixture.Explorer.Categories;
        TickOnly(fixture);

        Item(fixture, "Ducts").IsChecked = true;

        Assert.IsTrue(categories.IsolateCommand.CanExecute(null));
        Assert.IsNull(categories.MenuReason);
    }

    [TestMethod]
    public async Task SearchKeepsHiddenTicks()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var picker = fixture.Explorer.Categories.Picker;
        TickOnly(fixture, "Ducts");

        picker.Search = "Walls";

        Assert.IsTrue(Item(fixture, "Ducts").IsChecked);
        Assert.IsTrue(fixture.Explorer.Categories.IsolateCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task SetTicksBatchesOneChange()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var picker = fixture.Explorer.Categories.Picker;
        var changes = 0;
        picker.TicksChanged += (_, _) => changes++;

        picker.SetTicks([Item(fixture, "Ducts")], picker.Items.Where(item => item.Name != "Ducts").ToList());

        Assert.AreEqual(1, changes);
        Assert.AreEqual("Ducts", picker.Items.Single(item => item.IsChecked).Name);
        Assert.IsNull(fixture.FindGroup(Current, "Walls"));
    }

    [TestMethod]
    public async Task NoTickReportsReason()
    {
        using var fixture = await PanelFixture.OpenAsync();
        TickOnly(fixture);

        await fixture.Explorer.Categories.HalftoneCommand.ExecuteAsync(null);

        Assert.AreEqual(ExplorerCategories.SelectReason, fixture.Explorer.Message);
        Assert.IsEmpty(fixture.Actions.ExecutedPlans);
    }

    [TestMethod]
    public async Task IsolateShowsLinkNotice()
    {
        using var fixture = await PanelFixture.OpenAsync();
        TickOnly(fixture, "Ducts");
        fixture.Actions.Notice = VisibilityReasons.LinksKeepOwnSettings;

        await fixture.Explorer.Categories.IsolateCommand.ExecuteAsync(null);

        Assert.AreEqual(VisibilityReasons.LinksKeepOwnSettings, fixture.Explorer.Message);
    }
    [TestMethod]
    public async Task EyeHidesCategoryEverywhere()
    {
        using var fixture = await PanelFixture.OpenAsync();

        await Item(fixture, "Ducts").ToggleEyeCommand.ExecuteAsync(null);

        var step = (GroupEverywhereStep)fixture.Actions.ExecutedPlans.Single().Steps.Single();
        Assert.AreEqual(TestModel.Ducts, step.GroupId);
        Assert.IsTrue(Item(fixture, "Ducts").IsHiddenInView);
        Assert.IsFalse(Item(fixture, "Walls").IsHiddenInView);
    }

    [TestMethod]
    public async Task EyeTipNamesLinks()
    {
        using var fixture = await PanelFixture.OpenAsync();

        var tip = Item(fixture, "Ducts").EyeToolTip!;

        StringAssert.StartsWith(tip, "Hide in view: Current model + 3 links (By Host View)");
        StringAssert.Contains(tip, ExplorerCategories.NotReachedNote);
    }

    [TestMethod]
    public void TipWithoutLinksIsShort()
    {
        Assert.AreEqual("Show in view: Current model", ExplorerCategories.EyeTipOf(true, 0));
    }

    private static void TickOnly(PanelFixture fixture, params string[] names)
    {
        var picker = fixture.Explorer.Categories.Picker;
        picker.CheckNoneCommand.Execute(null);
        foreach (var name in names)
        {
            Item(fixture, name).IsChecked = true;
        }
    }

    private static CategoryItemViewModel Item(PanelFixture fixture, string name)
    {
        return fixture.Explorer.Categories.Picker.Items.First(item => item.Name == name);
    }
}
