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
    public void StatusHiddenWithoutSelection()
    {
        using var ui = PanelUi.Open();

        Assert.AreEqual(0, WpfSta.Invoke(() => ui.Panel.Explorer.SelectedCount), "The panel opened with selected rows.");
        Assert.IsTrue(ThemeHost.WaitForUi(() => ui.Host.TryFindInWindow("Explorer.Status") is null),
            "The status shows with nothing selected.");
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

        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Actions.ExecutedPlans.Any(plan => plan.Steps.Any(step => step is CategoryStep))),
            "The category eye did not run a category plan.");
        Assert.IsTrue(ThemeHost.WaitForUi(() => ui.Host.FindAllInWindow("Explorer.CategoryEye").First().ItemStatus == "Hidden"),
            "The category eye did not read back hidden.");
    }

    [TestMethod]
    public void CategoryMenuListsActions()
    {
        using var ui = PanelUi.Open();
        ui.Host.FindInWindow("Explorer.Categories").Patterns.Toggle.Pattern.Toggle();
        var menu = OpenCategoryMenu(ui);

        foreach (var id in new[] { "Isolate", "Halftone", "Color" })
        {
            Assert.IsNotNull(ui.Host.FindInMenu(menu, "Explorer.CategoryMenu." + id), id + " is missing.");
        }

        ui.Host.FindInMenu(menu, "Explorer.CategoryMenu.Isolate").Patterns.Invoke.Pattern.Invoke();

        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Actions.ExecutedPlans.Any(plan => plan.Action == VisibilityAction.Isolate)),
            "Isolate did not run on the ticked categories.");
    }

    [TestMethod]
    public void IsolateRunsOnTicked()
    {
        using var ui = PanelUi.Open();
        ui.Host.FindInWindow("Explorer.Categories").Patterns.Toggle.Pattern.Toggle();
        ui.Press("Explorer.CategoryNone");
        Assert.IsTrue(ThemeHost.WaitFor(() => SelectedCount(ui) == 0), "None left ticks.");
        var items = ui.Host.FindAllInWindow("Explorer.Category.Item");

        items[0].Patterns.Toggle.Pattern.Toggle();
        items[1].Patterns.Toggle.Pattern.Toggle();

        Assert.IsTrue(ThemeHost.WaitFor(() => SelectedCount(ui) == 2), "Two ticks did not stay.");
        var menu = OpenCategoryMenu(ui);
        ui.Host.FindInMenu(menu, "Explorer.CategoryMenu.Isolate").Patterns.Invoke.Pattern.Invoke();
        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Actions.ExecutedPlans.Any(plan =>
                plan.Action == VisibilityAction.Isolate && plan.Steps.Count(step => step is CategoryStep) == 2)),
            "Isolate did not run on both ticked categories.");
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
