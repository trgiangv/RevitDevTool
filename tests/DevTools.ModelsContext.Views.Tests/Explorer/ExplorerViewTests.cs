using DevTools.ModelsContext.Explorer;
using DevTools.ModelsContext.Selection;
using DevTools.ModelsContext.Tests.Shared;
using DevTools.ModelsContext.Visibility;
using DevTools.Presentation.Views.ModelsContext.Explorer;
using DevTools.Presentation.Views.ModelsContext.Panel;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace DevTools.ModelsContext.Views.Tests.Explorer;

[TestClass]
[DoNotParallelize]
public sealed class ExplorerViewTests
{
    [TestMethod]
    public void CategoriesBoxListsChecked()
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
    public void UncheckRemovesGroupRow()
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
            "Isolate did not run on the checked categories.");
    }

    [TestMethod]
    public void IsolateRunsOnChecked()
    {
        using var ui = PanelUi.Open();
        ui.Host.FindInWindow("Explorer.Categories").Patterns.Toggle.Pattern.Toggle();
        ui.Press("Explorer.CategoryNone");
        Assert.IsTrue(ThemeHost.WaitFor(() => SelectedCount(ui) == 0), "None left checks.");
        var items = ui.Host.FindAllInWindow("Explorer.Category.Item");

        items[0].Patterns.Toggle.Pattern.Toggle();
        items[1].Patterns.Toggle.Pattern.Toggle();

        Assert.IsTrue(ThemeHost.WaitFor(() => SelectedCount(ui) == 2), "Two checks did not stay.");
        var menu = OpenCategoryMenu(ui);
        ui.Host.FindInMenu(menu, "Explorer.CategoryMenu.Isolate").Patterns.Invoke.Pattern.Invoke();
        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Actions.ExecutedPlans.Any(plan =>
                plan.Action == VisibilityAction.Isolate && plan.Steps.Count(step => step is CategoryStep) == 2)),
            "Isolate did not run on both checked categories.");
    }
    [TestMethod]
    public void MenuOffWithoutChecks()
    {
        using var ui = PanelUi.Open();
        ui.Host.FindInWindow("Explorer.Categories").Patterns.Toggle.Pattern.Toggle();
        ui.Press("Explorer.CategoryNone");
        Assert.IsTrue(ThemeHost.WaitFor(() => SelectedCount(ui) == 0), "None left checks.");

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
    public void PickOnWithCheckedLink()
    {
        using var ui = PanelUi.Open();

        WpfSta.Invoke(() => ui.Panel.ContextBar.Check([FakeModelGenerator.Mep]));

        Assert.IsTrue(ThemeHost.WaitForUi(() => ui.Host.FindInWindow("Explorer.Pick").IsEnabled), "Pick stayed off with a link checked.");
        ui.Press("Explorer.Pick");
        Assert.IsTrue(ThemeHost.WaitForUi(() => ui.Host.TryFindInWindow("Explorer.PickStatus") is not null),
            "Pick left no status cue.");
    }

    [TestMethod]
    public void DimmedCountShowsInActiveView()
    {
        using var ui = PanelUi.Open();
        ui.ShowView("ModelContext.Scope.ActiveView");
        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Panel.Explorer.Rows.Any(row => row.DimmedCount > 0)), "No row counts hidden elements.");

        var row = ui.Host.Row("Ducts");

        Assert.IsTrue(ThemeHost.WaitForUi(() => ui.Has(row, "Explorer.Row.DimmedCount")), "The Ducts row has no hidden count.");
    }

    [TestMethod]
    public void SelectedDimmedRowIsFull()
    {
        using var ui = PanelUi.Open();
        ui.ShowView("ModelContext.Scope.ActiveView");
        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Panel.Explorer.Rows.Any(row => row.DimmedCount > 0)), "No row counts hidden elements.");
        List<TreeRow> dimmed = [];
        WpfSta.Run(async () =>
        {
            var explorer = ui.Panel.Explorer;
            var ducts = explorer.Rows.First(row => row is { Kind: TreeRowKind.Group, Text: "Ducts" } && row.Document == FakeModelGenerator.Current);
            await explorer.ExpandAsync(ducts);
            var family = explorer.Rows.First(row => row.Parent == ducts && row is { DimmedCount: > 1, Count: > 0 });
            await explorer.ExpandAsync(family);
            var type = explorer.Rows.First(row => row.Parent == family && row is { DimmedCount: > 1, Count: > 0 });
            await explorer.ExpandAsync(type);
            dimmed = explorer.Rows.Where(row => row.Parent == type && row.IsDimmed).Take(2).ToList();
        });
        Assert.HasCount(2, dimmed);

        WpfSta.Invoke(() =>
        {
            var list = RowList(ui);
            list.SelectedItems.Clear();
            list.SelectedItems.Add(dimmed[0]);
            list.ScrollIntoView(dimmed[1]);
        });

        Assert.IsTrue(ThemeHost.WaitFor(() => RowOpacity(ui, dimmed[0]) == 1), $"The selected dimmed row reads opacity {WpfSta.Invoke(() => RowOpacity(ui, dimmed[0]))}.");
        Assert.AreEqual(0.55, WpfSta.Invoke(() => RowOpacity(ui, dimmed[1])), 0.001, "The unselected dimmed row is not dimmed.");
        Assert.AreEqual(HideState.Hidden, dimmed[0].HideState);
    }

    [TestMethod]
    [DataRow(System.Windows.Input.Key.Z)]
    [DataRow(System.Windows.Input.Key.I)]
    [DataRow(System.Windows.Input.Key.H)]
    public void LetterKeyRunsOnRow(System.Windows.Input.Key key)
    {
        using var ui = PanelUi.Open();
        var ducts = WpfSta.Invoke(() => ui.Panel.Explorer.Rows.First(row => row is { Kind: TreeRowKind.Group, Text: "Ducts" }));
        WpfSta.Invoke(() => RowList(ui).SelectedItems.Add(ducts));

        WpfSta.Invoke(() => PressKey(ui, ducts, key));

        Assert.IsTrue(ThemeHost.WaitFor(() => key == System.Windows.Input.Key.Z
                ? ui.Actions.Zoomed.Count == 1
                : ui.Actions.ExecutedPlans.Any(plan => plan.Action == (key == System.Windows.Input.Key.I ? VisibilityAction.Isolate : VisibilityAction.Hide))),
            key + " did not run on the selected row.");
    }

    [TestMethod]
    public void DoubleClickZoomsInstance()
    {
        using var ui = PanelUi.Open();
        var instance = ExpandToInstance(ui);

        WpfSta.Invoke(() => DoubleClick(ui, instance, null));

        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Actions.Zoomed.Count == 1), "Double-click did not zoom.");
        Assert.AreEqual(instance.Id, ui.Actions.Zoomed[0].Single().Id);
        Assert.IsTrue(WpfSta.Invoke(() => RowList(ui).SelectedItems.Cast<TreeRow>().SequenceEqual([instance])),
            "Double-click did not select only the row.");
    }

    [TestMethod]
    public void DoubleClickExpandsGroup()
    {
        using var ui = PanelUi.Open();
        var ducts = WpfSta.Invoke(() => ui.Panel.Explorer.Rows.First(row => row is { Kind: TreeRowKind.Group, Text: "Ducts" }));

        WpfSta.Invoke(() => DoubleClick(ui, ducts, null));
        Assert.IsTrue(ThemeHost.WaitFor(() => ducts.IsExpanded), "Double-click did not open the group.");
        WpfSta.Invoke(() => DoubleClick(ui, ducts, null));

        Assert.IsTrue(ThemeHost.WaitFor(() => !ducts.IsExpanded), "Double-click did not close the group.");
        Assert.IsEmpty(ui.Actions.Zoomed);
    }

    [TestMethod]
    public void BusyEyeSwallowsClick()
    {
        using var ui = PanelUi.Open();
        var (ducts, other) = WpfSta.Invoke(() => (
            ui.Panel.Explorer.Rows.First(row => row is { Kind: TreeRowKind.Group, Text: "Ducts" }),
            ui.Panel.Explorer.Rows.First(row => row is { Kind: TreeRowKind.Group } && row.Text != "Ducts")));
        WpfSta.Invoke(ui.Model.HoldScopes);
        WpfSta.Invoke(() => { _ = ui.Panel.Explorer.ToggleHiddenCommand.ExecuteAsync(ducts); });
        Assert.IsTrue(ThemeHost.WaitFor(() => WpfSta.Invoke(() => ui.Model.HeldScopes) > 0), "The hide never started.");

        var (enabled, found, onText) = WpfSta.Invoke(() =>
        {
            var list = RowList(ui);
            list.UpdateLayout();
            var item = (System.Windows.Controls.ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(other);
            var presenter = FindPresenter(item)!;
            var eye = (System.Windows.Controls.Primitives.ButtonBase)presenter.ContentTemplate.FindName("Eye", presenter);
            var center = eye.TranslatePoint(new System.Windows.Point(eye.ActualWidth / 2, eye.ActualHeight / 2), item);
            var text = new System.Windows.Point(item.ActualWidth / 3, item.ActualHeight / 2);
            return (eye.IsEnabled, ReferenceEquals(RowButtons.ButtonAt(item, center), eye), RowButtons.ButtonAt(item, text));
        });
        WpfSta.Invoke(ui.Model.ReleaseScopes);
        Assert.IsTrue(ThemeHost.WaitFor(() => WpfSta.Invoke(() => !ui.Panel.Explorer.ToggleHiddenCommand.IsRunning)),
            "The hide did not finish.");

        Assert.IsFalse(enabled, "The eye stays enabled while the hide runs.");
        Assert.IsTrue(found, "The hit test misses the disabled eye, so its click would select the row.");
        Assert.IsNull(onText, "The row text reads as a button.");
    }

    [TestMethod]
    public void DoubleClickOnEyeSkipsRow()
    {
        using var ui = PanelUi.Open();
        var instance = ExpandToInstance(ui);

        WpfSta.Invoke(() => DoubleClick(ui, instance, "Eye"));

        Assert.IsFalse(ThemeHost.WaitFor(() => ui.Actions.Zoomed.Count > 0), "A double-click on the eye zoomed.");
    }

    private static TreeRow ExpandToInstance(PanelUi ui)
    {
        TreeRow? instance = null;
        WpfSta.Run(async () =>
        {
            var explorer = ui.Panel.Explorer;
            var parent = explorer.Rows.First(row => row is { Kind: TreeRowKind.Group, Text: "Ducts" } && row.Document == FakeModelGenerator.Current);
            while (instance is null)
            {
                await explorer.ExpandAsync(parent);
                var child = explorer.Rows.First(row => row.Parent == parent);
                instance = child.Kind == TreeRowKind.Instance ? child : null;
                parent = child;
            }
        });
        return instance!;
    }

    private static void DoubleClick(PanelUi ui, TreeRow row, string? part)
    {
        var list = RowList(ui);
        list.ScrollIntoView(row);
        list.UpdateLayout();
        var item = (System.Windows.Controls.ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(row);
        var presenter = FindPresenter(item)!;
        var source = part is null ? item : presenter.ContentTemplate.FindName(part, presenter);
        item.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left)
        {
            RoutedEvent = System.Windows.Controls.Control.MouseDoubleClickEvent,
            Source = source,
        });
    }

    private static void PressKey(PanelUi ui, TreeRow row, System.Windows.Input.Key key)
    {
        var list = RowList(ui);
        list.UpdateLayout();
        var item = (System.Windows.Controls.ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(row);
        var source = System.Windows.PresentationSource.FromVisual(item)!;
        foreach (var routed in new[] { System.Windows.Input.Keyboard.PreviewKeyDownEvent, System.Windows.Input.Keyboard.KeyDownEvent })
        {
            item.RaiseEvent(new System.Windows.Input.KeyEventArgs(NoModifierKeyboard.Device, source, 0, key) { RoutedEvent = routed });
        }
    }

    private static System.Windows.Controls.ListBox RowList(PanelUi ui)
    {
        var explorer = (ExplorerTabView)ui.View.FindName("ExplorerView");
        return (System.Windows.Controls.ListBox)explorer.FindName("RowList");
    }

    private static double RowOpacity(PanelUi ui, TreeRow row)
    {
        var list = RowList(ui);
        list.UpdateLayout();
        if (list.ItemContainerGenerator.ContainerFromItem(row) is not System.Windows.Controls.ListBoxItem item)
        {
            return double.NaN;
        }

        var presenter = FindPresenter(item);
        return presenter?.ContentTemplate.FindName("RowGrid", presenter) is System.Windows.UIElement grid ? grid.Opacity : double.NaN;
    }

    private static System.Windows.Controls.ContentPresenter? FindPresenter(System.Windows.DependencyObject node)
    {
        for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(node); index++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(node, index);
            if (child is System.Windows.Controls.ContentPresenter { ContentTemplate: not null } presenter)
            {
                return presenter;
            }

            if (FindPresenter(child) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    [TestMethod]
    public void SelectedRowKeepsNameAndEye()
    {
        using var ui = PanelUi.Open();
        ui.ShowView("ModelContext.Scope.ActiveView");
        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Panel.Explorer.Rows.Any(row => row.DimmedCount > 0)), "No row counts hidden elements.");
        TreeRow? type = null;
        WpfSta.Run(async () =>
        {
            var explorer = ui.Panel.Explorer;
            var ducts = explorer.Rows.First(row => row is { Kind: TreeRowKind.Group, Text: "Ducts" } && row.Document == FakeModelGenerator.Current);
            await explorer.ExpandAsync(ducts);
            var family = explorer.Rows.First(row => row.Parent == ducts && row is { DimmedCount: > 0, Count: > 0 });
            await explorer.ExpandAsync(family);
            type = explorer.Rows.First(row => row.Parent == family && row is { DimmedCount: > 0, Count: > 0 }
                                              && explorer.Rows.Count(other => other.Text == row.Text) == 1);
        });

        var list = ui.Host.FindInWindow("Explorer.Rows").BoundingRectangle;
        var item = ui.Host.Row(type!.Text);
        item.Patterns.SelectionItem.Pattern.Select();

        Assert.IsTrue(ThemeHost.WaitForUi(() => item.FindFirstDescendant(cf => cf.ByAutomationId("Explorer.Row.Isolate")) is { IsOffscreen: false }),
            "The row tools did not show on the selected row.");
        var text = item.FindFirstDescendant(cf => cf.ByAutomationId("Explorer.Row.Text"))!.BoundingRectangle;
        var eye = item.FindFirstDescendant(cf => cf.ByAutomationId("Explorer.Row.Eye"))!.BoundingRectangle;
        Assert.IsGreaterThanOrEqualTo(24, text.Width, $"The name keeps no characters: {text}.");
        Assert.IsTrue(text.Left >= list.Left && text.Right <= list.Right, $"The name {text} is not inside the list {list}.");
        var row = item.BoundingRectangle;
        Assert.IsTrue(row.Right <= list.Right, $"The row {row} is wider than the list {list}.");
        Assert.IsTrue(eye.Width >= 16 && eye.Left >= row.Left && eye.Right <= row.Right, $"The eye {eye} is clipped by the row {row}.");
        Assert.IsNull(item.FindFirstDescendant(cf => cf.ByAutomationId("Explorer.Row.Count")), "The count still takes width beside the row tools.");
    }
}
