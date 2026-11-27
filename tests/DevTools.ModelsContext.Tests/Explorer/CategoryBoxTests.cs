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
    public async Task CategoriesStartChecked()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var picker = fixture.Explorer.Categories.Picker;

        Assert.IsNotEmpty(picker.Items);
        Assert.IsTrue(picker.Items.All(item => item.IsChecked));
        Assert.AreEqual(CategoryPickerViewModel.AllCategories, picker.Summary);
    }

    [TestMethod]
    public async Task UncheckDropsGroupRow()
    {
        using var fixture = await PanelFixture.OpenAsync();

        Item(fixture, "Ducts").IsChecked = false;

        Assert.IsNull(fixture.FindGroup(Current, "Ducts"));
        Assert.IsNotNull(fixture.FindGroup(Current, "Walls"));
    }

    [TestMethod]
    public async Task ModelCountFollowsChecks()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var before = fixture.Explorer.Rows[0].Count;
        var ducts = fixture.Group(Current, "Ducts").Count;

        Item(fixture, "Ducts").IsChecked = false;

        Assert.AreEqual(before - ducts, fixture.Explorer.Rows[0].Count);
    }

    [TestMethod]
    public async Task NewCategoryShowsChecked()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var bar = fixture.Panel.ContextBar;
        bar.Check([FakeModelGenerator.ArchFirst]);
        bar.Documents.First(item => item.Key == Current).IsChecked = false;
        Assert.IsFalse(fixture.Explorer.Categories.Picker.Items.Any(item => item.Name == "Ducts"));
        Item(fixture, "Walls").IsChecked = false;

        bar.Check([Current]);

        Assert.IsTrue(Item(fixture, "Ducts").IsChecked);
        Assert.IsFalse(Item(fixture, "Walls").IsChecked);
    }

    [TestMethod]
    public async Task IsolateActsOnChecked()
    {
        using var fixture = await PanelFixture.OpenAsync();
        CheckOnly(fixture, "Ducts", "Walls");

        await fixture.Explorer.Categories.IsolateCommand.ExecuteAsync(null);

        var plan = fixture.Actions.ExecutedPlans.Single();
        Assert.AreEqual(VisibilityAction.Isolate, plan.Action);
        CollectionAssert.AreEquivalent(new[] { TestModel.Ducts, TestModel.Walls },
            plan.Steps.Cast<CategoryStep>().Select(step => step.CategoryId).ToList());
    }

    [TestMethod]
    public async Task HalftoneActsOnChecked()
    {
        using var fixture = await PanelFixture.OpenAsync();
        CheckOnly(fixture, "Ducts");

        await fixture.Explorer.Categories.HalftoneCommand.ExecuteAsync(null);

        var step = (CategoryStep)fixture.Actions.ExecutedPlans.Single().Steps.Single();
        Assert.AreEqual(TestModel.Ducts, step.CategoryId);
    }

    [TestMethod]
    public async Task NoCheckDisablesMenu()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var categories = fixture.Explorer.Categories;

        CheckOnly(fixture);

        Assert.IsFalse(categories.IsolateCommand.CanExecute(null));
        Assert.IsFalse(categories.HalftoneCommand.CanExecute(null));
        Assert.IsFalse(categories.ColorCommand.CanExecute(null));
        Assert.AreEqual(ExplorerCategories.SelectReason, categories.MenuReason);
    }

    [TestMethod]
    public async Task CheckEnablesMenu()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var categories = fixture.Explorer.Categories;
        CheckOnly(fixture);

        Item(fixture, "Ducts").IsChecked = true;

        Assert.IsTrue(categories.IsolateCommand.CanExecute(null));
        Assert.IsNull(categories.MenuReason);
    }

    [TestMethod]
    public async Task SearchKeepsHiddenChecks()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var picker = fixture.Explorer.Categories.Picker;
        CheckOnly(fixture, "Ducts");

        picker.Search = "Walls";

        Assert.IsTrue(Item(fixture, "Ducts").IsChecked);
        Assert.IsTrue(fixture.Explorer.Categories.IsolateCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task HideUncheckedListsCheckedOnly()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var picker = fixture.Explorer.Categories.Picker;
        CheckOnly(fixture, "Ducts", "Walls");

        picker.HideUnchecked = true;

        CollectionAssert.AreEquivalent(new[] { "Ducts", "Walls" }, Visible(picker));
    }

    [TestMethod]
    public async Task HideUncheckedCombinesWithSearch()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var picker = fixture.Explorer.Categories.Picker;
        CheckOnly(fixture, "Ducts", "Walls");
        picker.HideUnchecked = true;

        picker.Search = "Duct";

        CollectionAssert.AreEqual(new[] { "Ducts" }, Visible(picker));
    }

    [TestMethod]
    public async Task UncheckDropsHiddenRow()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var picker = fixture.Explorer.Categories.Picker;
        CheckOnly(fixture, "Ducts", "Walls");
        picker.HideUnchecked = true;

        Item(fixture, "Ducts").IsChecked = false;

        CollectionAssert.AreEqual(new[] { "Walls" }, Visible(picker));
    }

    [TestMethod]
    public async Task HideOffListsAllAgain()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var picker = fixture.Explorer.Categories.Picker;
        var all = Visible(picker);
        CheckOnly(fixture, "Ducts");
        picker.HideUnchecked = true;

        picker.HideUnchecked = false;

        CollectionAssert.AreEqual(all, Visible(picker));
    }

    [TestMethod]
    public async Task SetChecksBatchesOneChange()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var picker = fixture.Explorer.Categories.Picker;
        var changes = 0;
        picker.ChecksChanged += (_, _) => changes++;

        picker.SetChecks([Item(fixture, "Ducts")], picker.Items.Where(item => item.Name != "Ducts").ToList());

        Assert.AreEqual(1, changes);
        Assert.AreEqual("Ducts", picker.Items.Single(item => item.IsChecked).Name);
        Assert.IsNull(fixture.FindGroup(Current, "Walls"));
    }

    [TestMethod]
    public async Task NoCheckReportsReason()
    {
        using var fixture = await PanelFixture.OpenAsync();
        CheckOnly(fixture);

        await fixture.Explorer.Categories.HalftoneCommand.ExecuteAsync(null);

        Assert.AreEqual(ExplorerCategories.SelectReason, fixture.Panel.Message);
        Assert.IsEmpty(fixture.Actions.ExecutedPlans);
    }

    [TestMethod]
    public async Task IsolateShowsLinkNotice()
    {
        using var fixture = await PanelFixture.OpenAsync();
        CheckOnly(fixture, "Ducts");
        fixture.Actions.Notice = VisibilityText.LinksKeepOwnSettings;

        await fixture.Explorer.Categories.IsolateCommand.ExecuteAsync(null);

        Assert.AreEqual(VisibilityText.LinksKeepOwnSettings, fixture.Panel.Message);
    }
    [TestMethod]
    public async Task EyeHidesCategoryEverywhere()
    {
        using var fixture = await PanelFixture.OpenAsync();

        await Item(fixture, "Ducts").ToggleHiddenCommand.ExecuteAsync(null);

        var step = (CategoryStep)fixture.Actions.ExecutedPlans.Single().Steps.Single();
        Assert.AreEqual(TestModel.Ducts, step.CategoryId);
        Assert.IsTrue(Item(fixture, "Ducts").IsHidden);
        Assert.IsFalse(Item(fixture, "Walls").IsHidden);
    }

    [TestMethod]
    public async Task EyeTipNamesLinks()
    {
        using var fixture = await PanelFixture.OpenAsync();

        var tip = Item(fixture, "Ducts").HideHint!;

        StringAssert.StartsWith(tip, "Hide in view: Current model + 3 links (By Host View)");
        StringAssert.Contains(tip, ExplorerCategories.NotReachedNote);
    }

    [TestMethod]
    public void TipWithoutLinksIsShort()
    {
        Assert.AreEqual("Show in view: Current model", ExplorerCategories.HideTipOf(true, 0));
    }

    private static void CheckOnly(PanelFixture fixture, params string[] names)
    {
        var picker = fixture.Explorer.Categories.Picker;
        picker.CheckNoneCommand.Execute(null);
        foreach (var name in names)
        {
            Item(fixture, name).IsChecked = true;
        }
    }

    private static string[] Visible(CategoryPickerViewModel picker)
    {
        return picker.VisibleItems.Select(item => item.Name).ToArray();
    }

    private static CategoryItemViewModel Item(PanelFixture fixture, string name)
    {
        return fixture.Explorer.Categories.Picker.Items.First(item => item.Name == name);
    }
}
