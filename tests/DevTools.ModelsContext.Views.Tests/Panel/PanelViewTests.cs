using DevTools.ModelsContext.Panel;
using DevTools.ModelsContext.Tests.Shared;
using DevTools.Presentation.Views.ModelsContext.Explorer;
using DevTools.Presentation.Views.ModelsContext.Filters;
using DevTools.UI.Controls;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using AutomationElement = FlaUI.Core.AutomationElements.AutomationElement;

namespace DevTools.ModelsContext.Views.Tests.Panel;

[TestClass]
[DoNotParallelize]
public sealed class PanelViewTests
{
    [TestMethod]
    public void ViewSwitchShowsFilter()
    {
        using var ui = PanelUi.Open();

        ui.ShowView("ModelContext.View.Filter");

        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Panel.SelectedTab == PanelTab.Filter), "The switch did not pick Filter.");
        Assert.IsTrue(ThemeHost.WaitFor(() => IsShown(ui, "FilterView") && !IsShown(ui, "ExplorerView")), "The body did not show Filter.");
        Assert.IsNotNull(ui.Host.FindInWindow("Filter.Add"));
    }

    [TestMethod]
    public void ViewSwitchReturnsToExplorer()
    {
        using var ui = PanelUi.Open();
        ui.ShowView("ModelContext.View.Filter");
        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Panel.SelectedTab == PanelTab.Filter), "The switch did not pick Filter.");

        ui.ShowView("ModelContext.View.Explorer");

        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Panel.SelectedTab == PanelTab.Explorer), "The switch did not pick Explorer.");
        Assert.IsTrue(ThemeHost.WaitFor(() => IsShown(ui, "ExplorerView") && !IsShown(ui, "FilterView")), "The body did not show Explorer.");
    }

    [TestMethod]
    public void ScopeSwitchWorksThroughUia()
    {
        using var ui = PanelUi.Open();

        ui.ShowView("ModelContext.Scope.ActiveView");

        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Panel.ContextBar.Scope == DevTools.ModelsContext.Scope.ScopeKind.ActiveView),
            "The scope did not change.");
    }

    [TestMethod]
    public void SwitchRowFitsNarrowPane()
    {
        using var ui = PanelUi.Open();

        var (right, width) = WpfSta.Invoke(() =>
        {
            var view = (System.Windows.FrameworkElement)ui.View.FindName("SwitchRow");
            var bounds = view.TransformToAncestor(ui.View).TransformBounds(new System.Windows.Rect(view.RenderSize));
            return (bounds.Right, ui.View.ActualWidth);
        });

        Assert.IsLessThanOrEqualTo(width - 8, right, "The switch row runs past the pane.");
    }

    [TestMethod]
    public void SelectionSyncButtonToggles()
    {
        using var ui = PanelUi.Open();
        Assert.IsTrue(WpfSta.Invoke(() => ui.Panel.Explorer.IsSelectionSyncOn));

        ui.Host.FindInWindow("Explorer.SelectionSync").Patterns.Toggle.Pattern.Toggle();

        Assert.IsTrue(ThemeHost.WaitFor(() => !ui.Panel.Explorer.IsSelectionSyncOn), "Selection sync did not turn off.");
    }

    [TestMethod]
    public void SearchClearButtonEmptiesSearch()
    {
        using var ui = PanelUi.Open();
        ui.Host.FindInWindow("Explorer.Search").Patterns.Value.Pattern.SetValue("duct");
        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Panel.Explorer.SearchText == "duct"), "Search text did not reach Explorer.");

        WpfSta.Invoke(() =>
        {
            var search = (global::HandyControl.Controls.SearchBar)((ExplorerTabView)ui.View.FindName("ExplorerView")).FindName("SearchBox");
            var clear = (Button)search.Template.FindName("ButtonClear", search);
            Assert.IsTrue(clear.IsEnabled, "The clear button has no handler.");
            ((System.Windows.Input.RoutedCommand)clear.Command).Execute(clear.CommandParameter, clear);
        });

        Assert.IsTrue(ThemeHost.WaitFor(() => string.IsNullOrEmpty(ui.Panel.Explorer.SearchText)), "The clear button left the search text.");
    }

    [TestMethod]
    public void SearchRowSharesHeight()
    {
        using var ui = PanelUi.Open();

        var heights = WpfSta.Invoke(() =>
        {
            var explorer = (ExplorerTabView)ui.View.FindName("ExplorerView");
            explorer.UpdateLayout();
            return new[] { "Explorer.Search", "Explorer.SelectionSync" }
                .Select(id => Descendants(explorer).OfType<System.Windows.FrameworkElement>()
                    .First(element => System.Windows.Automation.AutomationProperties.GetAutomationId(element) == id).ActualHeight)
                .ToList();
        });

        Assert.AreEqual(heights[0], heights[1], "Search " + heights[0] + " vs selection sync " + heights[1]);
    }

    [TestMethod]
    public void ExpandAllButtonOpensTypes()
    {
        using var ui = PanelUi.Open();

        ui.Press("Explorer.ExpandAll");

        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Panel.Explorer.Rows.Any(row => row.Kind == DevTools.ModelsContext.Explorer.TreeRowKind.Type)),
            "Expand all did not open the types.");
    }

    [TestMethod]
    public void CollapseAllButtonLeavesRoots()
    {
        using var ui = PanelUi.Open();

        ui.Press("Explorer.CollapseAll");

        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Panel.Explorer.Rows.All(row => row.Level == 0)),
            "Collapse all left child rows.");
    }

    [TestMethod]
    public void UpdatingBarCentredInExplorer()
    {
        using var ui = PanelUi.Open();

        AssertBarCentred(ui, "DocumentsBox", "Explorer.CategoryBox");
    }

    [TestMethod]
    public void UpdatingBarCentredInFilter()
    {
        using var ui = PanelUi.Open();
        ui.ShowView("ModelContext.View.Filter");
        Assert.IsTrue(ThemeHost.WaitFor(() => IsShown(ui, "FilterView")), "The body did not show Filter.");

        AssertBarCentred(ui, "DocumentsBox", "Filter.Source");
    }

    [TestMethod]
    public void SelectionScopeShowsDocuments()
    {
        using var ui = PanelUi.Open();
        ui.ShowView("ModelContext.Scope.Selection");

        Assert.IsTrue(ThemeHost.WaitFor(() => IsShown(ui, "DocumentsBox")), "The documents box hid in Selection.");
    }

    [TestMethod]
    public void DuplicateNameShowsStockError()
    {
        using var ui = PanelUi.Open();
        WpfSta.Invoke(() =>
        {
            ui.Panel.Filter.Library.AddCommand.Execute(null);
            ui.Panel.Filter.Library.SaveCommand.Execute(null);
            ui.Panel.Filter.Library.AddCommand.Execute(null);
        });
        ui.ShowView("ModelContext.View.Filter");

        var hasError = WpfSta.Invoke(() =>
        {
            ui.Panel.Filter.Library.EditName = "New filter 1";
            ui.View.UpdateLayout();
            var box = (TextBox)((FilterTabView)ui.View.FindName("FilterView")).FindName("NameBox");
            return Validation.GetHasError(box);
        });

        Assert.IsTrue(hasError);
    }

    [TestMethod]
    public void TextIgnoresHostForeground()
    {
        using var ui = PanelUi.Open();
        WpfSta.Invoke(() => ui.Panel.Filter.Library.AddCommand.Execute(null));
        ui.ShowView("ModelContext.View.Filter");
        WpfSta.Invoke(() =>
        {
            ui.Panel.Filter.Library.CommitRename();
            ui.Panel.Filter.Root.AddConditionCommand.Execute(null);
            ui.Panel.Filter.Root.AddGroupCommand.Execute(null);
        });
        var sentinel = System.Windows.Media.Colors.Magenta;

        var leaks = WpfSta.Invoke(() =>
        {
            ui.Host.Window.Foreground = new System.Windows.Media.SolidColorBrush(sentinel);
            ((CategorySection)((FilterTabView)ui.View.FindName("FilterView")).FindName("CategorySection")).IsOpen = true;
            ui.View.UpdateLayout();
            return Descendants(ui.View).OfType<TextBlock>()
                .Where(text => text.IsVisible && text.Foreground is System.Windows.Media.SolidColorBrush { Color: var color } && color == sentinel)
                .Select(text => string.IsNullOrEmpty(text.Text) ? text.Name : text.Text)
                .ToList();
        });

        Assert.IsEmpty(leaks, "Text takes the host foreground: " + string.Join(", ", leaks));
    }

    [TestMethod]
    public void GroupHeaderCentresTitle()
    {
        using var ui = PanelUi.Open();
        ui.ShowView("ModelContext.View.Filter");
        ui.Press("Filter.Add");
        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Panel.Filter.Library.SelectedSource is { IsCustom: true }), "No custom filter was added.");
        ui.Press("Filter.Set.Add");
        Assert.IsTrue(ThemeHost.WaitForUi(() => ui.Host.TryFindInWindow("Filter.Set.Remove") is not null), "The group did not show.");

        var offsets = WpfSta.Invoke(() =>
        {
            ui.View.UpdateLayout();
            var remove = Descendants(ui.View).OfType<Button>()
                .First(button => System.Windows.Automation.AutomationProperties.GetAutomationId(button) == "Filter.Set.Remove" && button.IsVisible);
            var row = (System.Windows.FrameworkElement)System.Windows.Media.VisualTreeHelper.GetParent(remove);
            var middle = CentreY(remove, ui.View);
            return Descendants(row).OfType<TextBlock>()
                .Where(text => text.Text == "Group")
                .Select(text => Math.Abs(CentreY(text, ui.View) - middle))
                .ToList();
        });

        Assert.HasCount(1, offsets);
        Assert.IsTrue(offsets.All(offset => offset < 1), "Group title is off centre: " + string.Join(", ", offsets));
    }

    [TestMethod]
    public void GroupHeaderHasNoControls()
    {
        using var ui = PanelUi.Open();

        var header = ui.Host.Row("Current model");
        var group = ui.Host.Row("Ducts");

        foreach (var id in new[] { "Explorer.Row.Eye", "Explorer.Row.Isolate", "Explorer.Row.Halftone", "Explorer.Row.Color" })
        {
            Assert.IsFalse(ui.Has(header, id), "Header row has " + id);
        }

        Assert.IsTrue(ui.Has(group, "Explorer.Row.Eye"), "Group row has no eye.");
    }

    [TestMethod]
    public void EyeTogglesRow()
    {
        using var ui = PanelUi.Open();
        Assert.AreEqual("Shown", ui.Host.Row("Ducts").FindFirstDescendant(cf => cf.ByAutomationId("Explorer.Row.Eye"))?.ItemStatus);

        ui.PressIn(ui.Host.Row("Ducts"), "Explorer.Row.Eye");

        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Actions.ExecutedPlans.Count == 1), "The eye did not run a plan.");
        Assert.IsTrue(
            ThemeHost.WaitForUi(() => ui.Host.Row("Ducts").FindFirstDescendant(cf => cf.ByAutomationId("Explorer.Row.Eye"))?.ItemStatus == "Hidden"),
            "The row did not show the hidden eye.");
    }

    [TestMethod]
    public void MenuListsActions()
    {
        using var ui = PanelUi.Open();
        ui.ShowView("ModelContext.View.Filter");
        var menu = WpfSta.Invoke(() =>
        {
            var filter = (FilterTabView)ui.View.FindName("FilterView");
            var row = (Border)filter.FindName("ResultRow");
            ui.Panel.Filter.OpenResultMenu();
            row.ContextMenu!.PlacementTarget = row;
            row.ContextMenu.IsOpen = true;
            return row.ContextMenu;
        });

        Assert.IsTrue(ThemeHost.WaitFor(() => menu.IsOpen), "The menu did not open.");
        foreach (var id in new[] { "Zoom", "SectionBox", "Isolate", "HideShow", "Halftone", "Color" })
        {
            Assert.IsNotNull(ui.Host.FindInMenu(menu, "Menu." + id), id + " is missing.");
        }

        Assert.IsNull(ui.Host.TryFindInMenu(menu, "Menu.SelectInHost"), "Select shows while selection sync is on.");
    }

    [TestMethod]
    public void ResultToolsHaveNames()
    {
        using var ui = PanelUi.Open(340);
        ui.ShowView("ModelContext.View.Filter");

        foreach (var (id, name) in new[] { ("Isolate", "Isolate"), ("Halftone", "Halftone"), ("Color", "Color"), ("Eye", "Hide") })
        {
            Assert.AreEqual(name, ui.Host.FindInWindow("Filter.Result." + id).Name, id + " has the wrong name.");
        }

        Assert.IsNull(ui.Host.TryFindInWindow("Filter.Menu"), "The ⋯ button is still there.");
    }

    [TestMethod]
    public void ResultIsolateRunsPlan()
    {
        using var ui = PanelUi.Open();
        ui.ShowView("ModelContext.View.Filter");
        ui.Press("Filter.Add");
        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Panel.Filter.Library.SelectedSource is { IsCustom: true }), "No custom filter was added.");
        WpfSta.Invoke(() =>
        {
            ui.Panel.Filter.Categories.Items[0].IsChecked = true;
            ui.Clock.Advance(DevTools.ModelsContext.Filters.FilterRunner.SelectionSyncDelay);
        });
        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Panel.Filter.Runner.ResultCount > 0), "The filter found nothing.");

        ui.Press("Filter.Result.Isolate");

        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Actions.ExecutedPlans.Count == 1), "Isolate on the result ran no plan.");
    }

    [TestMethod]
    public void RowMenuListsActions()
    {
        using var ui = PanelUi.Open();
        var menu = ui.OpenRowMenu("Ducts");

        foreach (var id in new[] { "Zoom", "SectionBox", "Isolate", "HideShow", "Halftone", "Color" })
        {
            Assert.IsNotNull(ui.Host.FindInMenu(menu, "Menu." + id), id + " is missing.");
        }
    }

    [TestMethod]
    public void MenuItemsShowIcons()
    {
        using var ui = PanelUi.Open();
        var menu = ui.OpenRowMenu("Ducts");

        var missing = WpfSta.Invoke(() => menu.Items.OfType<MenuItem>()
            .Where(item => item.IsVisible && item.Icon is not System.Windows.Shapes.Path { Data: not null })
            .Select(item => item.Header?.ToString())
            .ToList());

        Assert.IsEmpty(missing, string.Join(", ", missing));
    }

    [TestMethod]
    public void MenuShowsShortcuts()
    {
        using var ui = PanelUi.Open();
        var menu = ui.OpenRowMenu("Ducts");

        var zoom = WpfSta.Invoke(() => menu.Items.OfType<MenuItem>().First(item => item.InputGestureText == "Z"));

        Assert.IsTrue(WpfSta.Invoke(() => zoom.ActualWidth > 0), "Zoom has no width.");
    }

    [TestMethod]
    public void RowHasNoMenuButton()
    {
        using var ui = PanelUi.Open();
        var row = ui.Host.Row("Ducts");

        row.Patterns.SelectionItem.Pattern.Select();

        Assert.IsTrue(ThemeHost.WaitForUi(() => ui.Has(row, "Explorer.Row.Color")));
        Assert.IsFalse(ui.Has(row, "Explorer.Row.Menu"));
    }

    [TestMethod]
    public void IsolateToolRunsPlan()
    {
        using var ui = PanelUi.Open();

        var row = ui.Host.Row("Ducts");
        row.Patterns.SelectionItem.Pattern.Select();
        ui.PressIn(row, "Explorer.Row.Isolate");

        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Actions.ExecutedPlans.Any(plan => plan.Action == DevTools.ModelsContext.Visibility.VisibilityAction.Isolate)),
            "The isolate tool did not run a plan.");
    }

    [TestMethod]
    public void RowColorOpensPicker()
    {
        using var ui = PanelUi.Open();
        var popup = WpfSta.Invoke(() => (System.Windows.Controls.Primitives.Popup)ui.View.FindName("ColorPopup"));
        var windows = WpfSta.Invoke(() => System.Windows.Application.Current.Windows.Count);

        var row = ui.Host.Row("Ducts");
        row.Patterns.SelectionItem.Pattern.Select();
        ui.PressIn(row, "Explorer.Row.Color");

        Assert.IsTrue(ThemeHost.WaitFor(() => popup.IsOpen), "The color picker did not open.");
        Assert.AreEqual(windows, WpfSta.Invoke(() => System.Windows.Application.Current.Windows.Count));
        var confirm = Retry.WhileNull(
            () => ui.Host.PopupRoot(popup)
                .FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName("Confirm"))),
            TimeSpan.FromSeconds(5), ignoreException: true).Result;
        Assert.IsNotNull(confirm, "The picker has no confirm button.");
        confirm.Patterns.Invoke.Pattern.Invoke();

        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Actions.ExecutedPlans.Any(plan => plan.Action == DevTools.ModelsContext.Visibility.VisibilityAction.Color)),
            "Confirming a color did not run a color plan.");
    }
    [TestMethod]
    public void ColoredRowShowsSwatch()
    {
        using var ui = PanelUi.Open();
        WpfSta.Run(async () =>
        {
            var ducts = ui.Panel.Explorer.Rows.First(row => row.Text == "Ducts" && row.Kind == DevTools.ModelsContext.Explorer.TreeRowKind.Group);
            await ui.Panel.Explorer.RunToolAsync(ducts, MenuAction.Color);
            await ui.Panel.ContextMenu.ApplyColorCommand.ExecuteAsync(new DevTools.ModelsContext.Visibility.RgbColor(255, 0, 16));
        });

        ui.Host.Row("Walls").Patterns.SelectionItem.Pattern.Select();

        Assert.IsTrue(
            ThemeHost.WaitForUi(() => ui.Host.Row("Ducts").FindFirstDescendant(cf => cf.ByAutomationId("Explorer.Row.Color"))?.ItemStatus == "#FF0010"),
            "The colored row did not keep its swatch.");
        Assert.IsFalse(ui.Has(ui.Host.Row("Duct Fittings"), "Explorer.Row.Color"), "An uncolored row shows its color tool.");
    }

    [TestMethod]
    public void ChecksShowAsTags()
    {
        using var ui = PanelUi.Open();
        var box = DocumentsBox(ui);
        var keys = new[] { FakeModelGenerator.ArchFirst, FakeModelGenerator.ArchSecond, FakeModelGenerator.Mep };

        WpfSta.Invoke(() => ui.Panel.ContextBar.Check(keys));

        Assert.IsTrue(ThemeHost.WaitFor(() => box.SelectedItems.Count == 4), "The box did not check every document.");
        WpfSta.Invoke(() =>
        {
            box.UpdateLayout();
            var labels = ui.Panel.ContextBar.Documents.Where(item => item.IsChecked).Select(item => item.Label).ToList();
            var tags = Tags(box).Take(labels.Count - box.HiddenTagCount).ToList();
            Assert.IsNotEmpty(tags, "No tag is shown.");
            foreach (var tag in tags)
            {
                CollectionAssert.Contains(labels, tag.Content?.ToString());
                var right = tag.TranslatePoint(new System.Windows.Point(tag.ActualWidth, 0), box).X;
                Assert.IsTrue(right <= box.ActualWidth, tag.Content + " runs past the box. right=" + right);
            }
        });
    }

    [TestMethod]
    public void AllSkipsUnloadedLink()
    {
        using var ui = PanelUi.Open();
        var box = DocumentsBox(ui);
        WpfSta.Invoke(() => ui.Panel.ContextBar.SetDocuments(ui.Panel.ContextBar.Documents
            .Select(item => item.Info with { IsLoaded = item.Key != FakeModelGenerator.Mep })
            .ToList()));

        ToggleAll(ui);

        Assert.IsTrue(ThemeHost.WaitFor(() => box.SelectedItems.Count == 3), "All did not check only the loaded documents.");
        WpfSta.Invoke(() =>
        {
            var mep = ui.Panel.ContextBar.Documents.Single(item => item.Key == FakeModelGenerator.Mep);
            Assert.IsFalse(mep.IsChecked, "All checked the unloaded link.");
            Assert.IsFalse(box.IsItemEnabled(mep), "The unloaded link is not disabled.");
        });
    }

    [TestMethod]
    public void AllOffKeepsFirstDocument()
    {
        using var ui = PanelUi.Open();
        var box = DocumentsBox(ui);
        ToggleAll(ui);
        Assert.IsTrue(ThemeHost.WaitFor(() => box.SelectedItems.Count == 4), "All did not check every document.");

        ToggleAll(ui);

        Assert.IsTrue(ThemeHost.WaitFor(() => box.SelectedItems.Count == 1), "All off did not leave one document.");
        CollectionAssert.AreEqual(new[] { FakeModelGenerator.Current }, WpfSta.Invoke(() => ui.Panel.ContextBar.CheckedDocuments.ToList()));
    }

    [TestMethod]
    public void LastTagCannotClose()
    {
        using var ui = PanelUi.Open();
        var box = DocumentsBox(ui);
        Assert.IsTrue(ThemeHost.WaitFor(() => box.SelectedItems.Count == 1), "The current model is not checked.");

        CloseTag(ui, ContextBarViewModel.CurrentModelLabel);

        Assert.AreEqual(1, WpfSta.Invoke(() => box.SelectedItems.Count), "Closing the last tag changed the selection.");
        CollectionAssert.AreEqual(new[] { FakeModelGenerator.Current }, WpfSta.Invoke(() => ui.Panel.ContextBar.CheckedDocuments.ToList()));
        Assert.IsNotNull(TagRemove(ui, ContextBarViewModel.CurrentModelLabel), "The last tag disappeared.");
    }

    [TestMethod]
    public void ClosedBoxUnchecksCurrentModel()
    {
        using var ui = PanelUi.Open();
        var box = DocumentsBox(ui);
        ToggleItem(ui, WpfSta.Invoke(() => ui.Panel.ContextBar.LabelOf(FakeModelGenerator.Mep)));
        Assert.IsTrue(ThemeHost.WaitFor(() => box.SelectedItems.Count == 2), "The link did not check.");

        ToggleItem(ui, ContextBarViewModel.CurrentModelLabel);

        Assert.IsFalse(WpfSta.Invoke(() => box.IsDropDownOpen), "The drop-down opened.");
        Assert.IsTrue(ThemeHost.WaitFor(() => box.SelectedItems.Count == 1), "Current model did not uncheck.");
        CollectionAssert.AreEqual(new[] { FakeModelGenerator.Mep }, WpfSta.Invoke(() => ui.Panel.ContextBar.CheckedDocuments.ToList()));
    }

    [TestMethod]
    public void TagCloseUnchecksCurrentModel()
    {
        using var ui = PanelUi.Open();
        var box = DocumentsBox(ui);
        WpfSta.Invoke(() => ui.Panel.ContextBar.Check([FakeModelGenerator.Mep]));
        Assert.IsTrue(ThemeHost.WaitFor(() => box.SelectedItems.Count == 2), "The link did not check.");

        CloseTag(ui, ContextBarViewModel.CurrentModelLabel);

        Assert.IsTrue(ThemeHost.WaitFor(() => box.SelectedItems.Count == 1), "The current model tag did not close.");
        CollectionAssert.AreEqual(new[] { FakeModelGenerator.Mep }, WpfSta.Invoke(() => ui.Panel.ContextBar.CheckedDocuments.ToList()));
    }

    [TestMethod]
    public void TagCloseUnchecksLink()
    {
        using var ui = PanelUi.Open();
        var box = DocumentsBox(ui);
        WpfSta.Invoke(() => ui.Panel.ContextBar.Check([FakeModelGenerator.Mep]));
        Assert.IsTrue(ThemeHost.WaitFor(() => box.SelectedItems.Count == 2), "The link did not check.");

        CloseTag(ui, WpfSta.Invoke(() => ui.Panel.ContextBar.LabelOf(FakeModelGenerator.Mep)));

        Assert.IsTrue(ThemeHost.WaitFor(() => box.SelectedItems.Count == 1), "The link tag did not close.");
        CollectionAssert.AreEqual(new[] { FakeModelGenerator.Current }, WpfSta.Invoke(() => ui.Panel.ContextBar.CheckedDocuments.ToList()));
    }

    [TestMethod]
    public void TemporaryBarShowsAfterHide()
    {
        using var ui = PanelUi.Open();
        Assert.IsNull(ui.Host.TryFindInWindow("Temporary.Keep"), "The temporary bar shows before any hide.");

        ui.PressIn(ui.Host.Row("Ducts"), "Explorer.Row.Eye");

        Assert.IsNotNull(ui.Host.FindInWindow("Temporary.Keep"));
        Assert.IsNotNull(ui.Host.FindInWindow("Temporary.Reset"));
    }

    [TestMethod]
    public void TemporaryBarShowsInLight()
    {
        using var ui = PanelUi.Open(theme: DevTools.UI.Theme.AppTheme.Light);

        ui.PressIn(ui.Host.Row("Ducts"), "Explorer.Row.Eye");

        Assert.AreEqual(DevTools.UI.Theme.AppTheme.Light, WpfSta.Invoke(() => DevTools.UI.Theme.ThemeManager.Current.ActualApplicationTheme));
        Assert.IsNotNull(ui.Host.FindInWindow("Temporary.Reset"));
    }

    [TestMethod]
    public void KeepAsksForLinkChanges()
    {
        using var ui = PanelUi.Open();
        WpfSta.Invoke(() => ui.Panel.ContextBar.Check([FakeModelGenerator.Mep]));
        Assert.IsTrue(
            ThemeHost.WaitFor(() => ui.Panel.Explorer.Rows.Any(row => row.Text == "MEP-Mech.rvt")),
            "The link did not appear in Explorer.");
        ui.Collapse("Current model");

        ui.PressIn(ui.Host.Row("Ducts"), "Explorer.Row.Eye");
        ui.Press("Temporary.Keep");

        Assert.IsNotNull(ui.Host.FindInWindow("Temporary.ConfirmKeep"));
        Assert.AreEqual(0, ui.Actions.KeepCount);

        ui.Press("Temporary.ConfirmKeep");

        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Actions.KeepCount == 1), "Keep did not run after the confirm.");
    }

    [TestMethod]
    public void LinkedRowShowsColorTool()
    {
        using var ui = PanelUi.Open();
        WpfSta.Invoke(() => ui.Panel.ContextBar.Check([FakeModelGenerator.Mep]));
        Assert.IsTrue(
            ThemeHost.WaitFor(() => ui.Panel.Explorer.Rows.Any(row => row.Text == "Ducts" && row.IsInsideLink)),
            "The link rows did not appear in Explorer.");
        ui.Collapse("Current model");

        var row = ui.Host.Row("Ducts");
        row.Patterns.SelectionItem.Pattern.Select();

        Assert.IsTrue(ThemeHost.WaitForUi(() => ui.Has(row, "Explorer.Row.Color")), "A selected linked row has no color tool.");
        Assert.IsTrue(ui.Has(row, "Explorer.Row.Halftone"), "A selected linked row has no halftone tool.");
    }

    private static bool IsShown(PanelUi ui, string name)
    {
        return ((System.Windows.UIElement)ui.View.FindName(name)).IsVisible;
    }

    private static void AssertBarCentred(PanelUi ui, string aboveName, string belowId)
    {
        var (bar, above, below) = WpfSta.Invoke(() =>
        {
            var progress = (ProgressBar)ui.View.FindName("UpdatingBar");
            progress.Visibility = System.Windows.Visibility.Visible;
            ui.View.UpdateLayout();
            var next = Descendants(ui.View).OfType<System.Windows.FrameworkElement>()
                .First(element => element.IsVisible && System.Windows.Automation.AutomationProperties.GetAutomationId(element) == belowId);
            return (BoundsOf(progress, ui.View), BoundsOf((System.Windows.FrameworkElement)ui.View.FindName(aboveName), ui.View), BoundsOf(next, ui.View));
        });

        Assert.IsGreaterThan(0, bar.Height);
        Assert.IsGreaterThanOrEqualTo(above.Bottom, bar.Top, "The updating bar overlaps " + aboveName);
        Assert.IsLessThanOrEqualTo(below.Top, bar.Bottom, "The updating bar overlaps " + belowId);
        var gapMiddle = (above.Bottom + below.Top) / 2;
        var barMiddle = (bar.Top + bar.Bottom) / 2;
        Assert.IsLessThan(0.5, Math.Abs(gapMiddle - barMiddle), $"The bar sits at {barMiddle}, the gap middle is {gapMiddle}.");
    }

    private static System.Windows.Rect BoundsOf(System.Windows.FrameworkElement element, System.Windows.Media.Visual root)
    {
        return element.TransformToAncestor(root).TransformBounds(new System.Windows.Rect(element.RenderSize));
    }

    private static double CentreY(System.Windows.FrameworkElement element, System.Windows.Media.Visual root)
    {
        var bounds = BoundsOf(element, root);
        return (bounds.Top + bounds.Bottom) / 2;
    }

    private static IEnumerable<System.Windows.DependencyObject> Descendants(System.Windows.DependencyObject root)
    {
        var queue = new Queue<System.Windows.DependencyObject>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            yield return current;
            for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(current); index++)
            {
                queue.Enqueue(System.Windows.Media.VisualTreeHelper.GetChild(current, index));
            }
        }
    }

    private static TagCheckComboBox DocumentsBox(PanelUi ui)
    {
        return WpfSta.Invoke(() => (TagCheckComboBox)ui.View.FindName("DocumentsBox"));
    }

    private static List<TagCheckComboBoxTag> Tags(TagCheckComboBox box)
    {
        var overflow = box.PartAutomationId(TagCheckComboBox.MoreIdSuffix);
        return ((System.Windows.Controls.Panel)box.Template.FindName("PART_Panel", box)).Children.OfType<TagCheckComboBoxTag>()
            .Where(tag => System.Windows.Automation.AutomationProperties.GetAutomationId(tag) != overflow)
            .ToList();
    }

    private static AutomationElement Box(PanelUi ui)
    {
        return ui.Host.FindInWindow("ModelContext.Documents");
    }

    private static void ToggleAll(PanelUi ui)
    {
        var box = Box(ui);
        box.Patterns.ExpandCollapse.Pattern.Expand();
        var all = Retry.WhileNull(
            () => box.FindFirstDescendant(cf => cf.ByAutomationId("ModelContext.Documents.SelectAll")),
            TimeSpan.FromSeconds(5), ignoreException: true).Result;
        Assert.IsNotNull(all, "The box has no All toggle.");
        all.Patterns.Toggle.Pattern.Toggle();
    }

    private static void ToggleItem(PanelUi ui, string label)
    {
        var item = Retry.WhileNull(
            () => Box(ui).FindFirstDescendant(cf => cf.ByControlType(ControlType.ListItem).And(cf.ByName(label))),
            TimeSpan.FromSeconds(5), ignoreException: true).Result;
        Assert.IsNotNull(item, label + " is not in the list.");
        item.Patterns.Toggle.Pattern.Toggle();
    }

    private static AutomationElement? TagRemove(PanelUi ui, string label)
    {
        return Retry.WhileNull(
            () => Box(ui).FindFirstDescendant(cf => cf.ByAutomationId("ModelContext.Documents.TagRemove").And(cf.ByName("Remove " + label))),
            TimeSpan.FromSeconds(5), ignoreException: true).Result;
    }

    private static void CloseTag(PanelUi ui, string label)
    {
        var remove = TagRemove(ui, label);
        Assert.IsNotNull(remove, label + " has no remove button.");
        remove.Patterns.Invoke.Pattern.Invoke();
        WpfSta.Current.Invoke(() => { }, DispatcherPriority.ContextIdle);
    }
}
