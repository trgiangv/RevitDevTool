using DevTools.ModelsContext.Explorer;
using DevTools.ModelsContext.Selection;
using DevTools.ModelsContext.Tests.Shared;
using DevTools.ModelsContext.Visibility;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace DevTools.ModelsContext.Views.Tests.Explorer;

[TestClass]
[DoNotParallelize]
public sealed class ExplorerViewTests
{
    [TestMethod]
    public void CategoriesBoxListsTicked()
    {
        using var ui = PanelUi.Open();

        ui.Host.FindInWindow("Explorer.Categories").Patterns.Toggle.Pattern.Toggle();

        var list = ui.Host.FindInWindow("Explorer.CategoryList");
        var items = list.FindAllDescendants(cf => cf.ByAutomationId("Explorer.Category.Item"));
        Assert.IsNotEmpty(items);
        Assert.IsTrue(items.All(item => item.Patterns.Toggle.Pattern.ToggleState.Value == ToggleState.On));
    }

    [TestMethod]
    public void UntickRemovesGroupRow()
    {
        using var ui = PanelUi.Open();
        ui.Host.FindInWindow("Explorer.Categories").Patterns.Toggle.Pattern.Toggle();
        ui.Press("Explorer.CategoryNone");

        Assert.IsTrue(ThemeHost.WaitFor(() => !ui.Panel.Explorer.Rows.Any(row => row.Kind == TreeRowKind.Group)),
            "None left group rows in the tree.");
    }

    [TestMethod]
    public void CategoryEyeHidesCategory()
    {
        using var ui = PanelUi.Open();
        ui.Host.FindInWindow("Explorer.Categories").Patterns.Toggle.Pattern.Toggle();
        var eye = ui.Host.FindAllInWindow("Explorer.CategoryEye").First();
        Assert.AreEqual("Shown", eye.ItemStatus);

        eye.AsButton().Invoke();

        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Actions.ExecutedPlans.Any(plan => plan.Steps.Any(step => step is GroupEverywhereStep))),
            "The category eye did not run a category plan.");
        Assert.IsTrue(ThemeHost.WaitForUi(() => ui.Host.FindAllInWindow("Explorer.CategoryEye").First().ItemStatus == "Hidden"),
            "The category eye did not read back hidden.");
    }

    [TestMethod]
    public void CategoryMenuListsActions()
    {
        using var ui = PanelUi.Open();
        ui.Host.FindInWindow("Explorer.Categories").Patterns.Toggle.Pattern.Toggle();
        ui.Host.FindAllInWindow("Explorer.Category.Row").First().Patterns.SelectionItem.Pattern.Select();
        var menu = OpenCategoryMenu(ui);

        foreach (var id in new[] { "Isolate", "Halftone", "Color" })
        {
            Assert.IsNotNull(ui.Host.FindInMenu(menu, "Explorer.CategoryMenu." + id), id + " is missing.");
        }

        ui.Host.FindInMenu(menu, "Explorer.CategoryMenu.Isolate").Patterns.Invoke.Pattern.Invoke();

        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Actions.ExecutedPlans.Any(plan => plan.Action == VisibilityAction.Isolate)),
            "Isolate did not run on the selected category.");
    }

    [TestMethod]
    public void CategoryListSelectsMany()
    {
        using var ui = PanelUi.Open();
        ui.Host.FindInWindow("Explorer.Categories").Patterns.Toggle.Pattern.Toggle();

        var list = ui.Host.FindInWindow("Explorer.CategoryList");

        Assert.IsTrue(list.Patterns.Selection.Pattern.CanSelectMultiple.Value);
    }

    [TestMethod]
    public void AddToSelectionKeepsBoth()
    {
        using var ui = PanelUi.Open();
        ui.Host.FindInWindow("Explorer.Categories").Patterns.Toggle.Pattern.Toggle();
        var rows = ui.Host.FindAllInWindow("Explorer.Category.Row");

        rows[0].Patterns.SelectionItem.Pattern.Select();
        rows[1].Patterns.SelectionItem.Pattern.AddToSelection();

        Assert.IsTrue(ThemeHost.WaitFor(() => SelectedCount(ui) == 2), "Two rows were not selected.");
        var menu = OpenCategoryMenu(ui);
        ui.Host.FindInMenu(menu, "Explorer.CategoryMenu.Isolate").Patterns.Invoke.Pattern.Invoke();
        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Actions.ExecutedPlans.Any(plan =>
                plan.Action == VisibilityAction.Isolate && plan.Steps.Count(step => step is GroupEverywhereStep) == 2)),
            "Isolate did not run on both selected categories.");
    }

    [TestMethod]
    public void MenuOffWithoutTicks()
    {
        using var ui = PanelUi.Open();
        ui.Host.FindInWindow("Explorer.Categories").Patterns.Toggle.Pattern.Toggle();
        ui.Press("Explorer.CategoryNone");
        Assert.IsTrue(ThemeHost.WaitFor(() => SelectedCount(ui) == 0), "None left ticks.");

        var menu = OpenCategoryMenu(ui);

        var isolate = ui.Host.FindInMenu(menu, "Explorer.CategoryMenu.Isolate");
        Assert.IsFalse(isolate.IsEnabled);
        Assert.AreEqual(ExplorerCategories.SelectReason, isolate.HelpText);
    }

    [TestMethod]
    public void SelectRowTicksOnlyIt()
    {
        using var ui = PanelUi.Open();
        ui.Host.FindInWindow("Explorer.Categories").Patterns.Toggle.Pattern.Toggle();
        var name = WpfSta.Invoke(() => ui.Panel.Explorer.Categories.Picker.VisibleItems[1].Name);
        Assert.IsTrue(ThemeHost.WaitFor(() => HasGroup(ui, name)), name + " has no group row.");

        ui.Host.FindAllInWindow("Explorer.Category.Row")[0].Patterns.SelectionItem.Pattern.Select();

        Assert.IsTrue(ThemeHost.WaitFor(() => SelectedCount(ui) == 1), "Selecting one row did not leave one tick.");
        Assert.IsTrue(WpfSta.Invoke(() => ui.Panel.Explorer.Categories.Picker.VisibleItems[0].IsChecked));
        Assert.IsTrue(ThemeHost.WaitFor(() => !HasGroup(ui, name)), "The unselected row kept its group row.");
    }

    [TestMethod]
    public void TickSelectsRow()
    {
        using var ui = PanelUi.Open();
        ui.Host.FindInWindow("Explorer.Categories").Patterns.Toggle.Pattern.Toggle();
        ui.Press("Explorer.CategoryNone");
        Assert.IsTrue(ThemeHost.WaitForUi(() => !ui.Host.FindAllInWindow("Explorer.Category.Row")
            .Any(row => row.Patterns.SelectionItem.Pattern.IsSelected.Value)), "None left rows selected.");

        ui.Host.FindAllInWindow("Explorer.Category.Item")[1].Patterns.Toggle.Pattern.Toggle();

        Assert.IsTrue(ThemeHost.WaitForUi(() =>
                ui.Host.FindAllInWindow("Explorer.Category.Row")[1].Patterns.SelectionItem.Pattern.IsSelected.Value),
            "The tick did not select its row.");
        Assert.AreEqual(1, SelectedCount(ui));
    }
    private static bool HasGroup(PanelUi ui, string name)
    {
        return WpfSta.Invoke(() => ui.Panel.Explorer.Rows.Any(row => row.Kind == TreeRowKind.Group && row.Text == name));
    }

    private static int SelectedCount(PanelUi ui)
    {
        return WpfSta.Invoke(() => ui.Panel.Explorer.Categories.Picker.Items.Count(item => item.IsChecked));
    }

    private static System.Windows.Controls.ContextMenu OpenCategoryMenu(PanelUi ui)
    {
        var menu = WpfSta.Invoke(() =>
        {
            var explorer = (DevTools.Presentation.Views.ModelsContext.Explorer.ExplorerTabView)ui.View.FindName("ExplorerView");
            var section = (DevTools.Presentation.Views.ModelsContext.Filters.CategorySection)explorer.FindName("CategorySection");
            var list = (System.Windows.Controls.ListBox)section.FindName("CategoryList");
            list.ContextMenu!.PlacementTarget = list;
            list.ContextMenu.IsOpen = true;
            return list.ContextMenu;
        });
        Assert.IsTrue(ThemeHost.WaitFor(() => menu.IsOpen), "The category menu did not open.");
        return menu;
    }

    [TestMethod]
    public void PickOffWithoutLink()
    {
        using var ui = PanelUi.Open();

        var pick = ui.Host.FindInWindow("Explorer.Pick");

        Assert.IsFalse(pick.IsEnabled);
        Assert.AreEqual(PickController.NoLinkReason, pick.HelpText);
    }

    [TestMethod]
    public void PickOnWithTickedLink()
    {
        using var ui = PanelUi.Open();

        WpfSta.Invoke(() => ui.Panel.ContextBar.Tick([FakeModelGenerator.Mep]));

        Assert.IsTrue(ThemeHost.WaitForUi(() => ui.Host.FindInWindow("Explorer.Pick").IsEnabled), "Pick stayed off with a link ticked.");
        ui.Press("Explorer.Pick");
        Assert.IsTrue(ThemeHost.WaitForUi(() => ui.Host.TryFindInWindow("Explorer.PickStatus") is not null),
            "Pick left no status cue.");
    }

    [TestMethod]
    public void HiddenCountShowsInActiveView()
    {
        using var ui = PanelUi.Open();
        ui.ShowView("ModelContext.Scope.ActiveView");
        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Panel.Explorer.Rows.Any(row => row.HiddenCount > 0)), "No row counts hidden elements.");

        var row = ui.Host.Row("Ducts");

        Assert.IsTrue(ThemeHost.WaitForUi(() => ui.Has(row, "Explorer.Row.HiddenCount")), "The Ducts row has no hidden count.");
    }
}
