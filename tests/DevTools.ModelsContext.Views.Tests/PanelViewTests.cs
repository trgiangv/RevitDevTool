using DevTools.ModelsContext.Tests.Shared;
using DevTools.ModelsContext.ViewModels;
using DevTools.Presentation.Views.ModelsContext;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using CheckComboBox = HandyControl.Controls.CheckComboBox;
using Tag = HandyControl.Controls.Tag;

namespace DevTools.ModelsContext.Views.Tests;

[TestClass]
[DoNotParallelize]
public sealed class PanelViewTests
{
    [TestMethod]
    public void TabsAreExplorerAndFilter()
    {
        using var ui = PanelUi.Open();

        var tabs = ui.Host.FindInWindow("ModelContext.Tabs")
            .FindAllDescendants(cf => cf.ByControlType(ControlType.TabItem))
            .Select(tab => tab.Name)
            .ToArray();

        CollectionAssert.AreEqual(new[] { "Explorer", "Filter" }, tabs);
    }

    [TestMethod]
    public void UpdatingBarOverlapsNothing()
    {
        using var ui = PanelUi.Open();

        var (bar, others) = WpfSta.Invoke(() =>
        {
            var progress = (ProgressBar)ui.View.FindName("UpdatingBar");
            progress.Visibility = System.Windows.Visibility.Visible;
            ui.View.UpdateLayout();
            System.Windows.Rect BoundsOf(string name)
            {
                var element = (System.Windows.FrameworkElement)ui.View.FindName(name);
                return element.TransformToAncestor(ui.View)
                    .TransformBounds(new System.Windows.Rect(element.RenderSize));
            }

            return (BoundsOf("UpdatingBar"), new[] { BoundsOf("Tabs"), BoundsOf("DocumentsBox") });
        });

        Assert.IsGreaterThan(0, bar.Height);
        foreach (var other in others)
        {
            Assert.IsFalse(bar.IntersectsWith(other) && System.Windows.Rect.Intersect(bar, other).Height > 0,
                "The updating bar overlaps " + other);
        }
    }

    [TestMethod]
    public void GroupHeaderHasNoControls()
    {
        using var ui = PanelUi.Open();
        ui.Collapse("All models");

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
        ui.Collapse("All models");
        Assert.AreEqual("Shown", ui.Host.Row("Ducts").FindFirstDescendant(cf => cf.ByAutomationId("Explorer.Row.Eye"))?.ItemStatus);

        ui.PressIn(ui.Host.Row("Ducts"), "Explorer.Row.Eye");

        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Model.ExecutedPlans.Count == 1), "The eye did not run a plan.");
        Assert.IsTrue(
            ThemeHost.WaitForUi(() => ui.Host.Row("Ducts").FindFirstDescendant(cf => cf.ByAutomationId("Explorer.Row.Eye"))?.ItemStatus == "Hidden"),
            "The row did not show the hidden eye.");
    }

    [TestMethod]
    public void MenuListsActions()
    {
        using var ui = PanelUi.Open();
        ui.ShowTab("ModelContext.Tab.Filter");
        var menu = WpfSta.Invoke(() =>
        {
            var tabs = (TabControl)ui.View.FindName("Tabs");
            var filter = (FilterTabView)((TabItem)tabs.Items[1]).Content;
            return ((Border)filter.FindName("ResultRow")).ContextMenu!;
        });

        ui.Press("Filter.Menu");

        Assert.IsTrue(ThemeHost.WaitFor(() => menu.IsOpen), "The menu did not open.");
        foreach (var id in new[] { "Zoom", "SectionBox", "Isolate", "HideShow", "Halftone", "Color" })
        {
            Assert.IsNotNull(ui.Host.FindInMenu(menu, "Menu." + id), id + " is missing.");
        }

        Assert.IsNull(ui.Host.TryFindInMenu(menu, "Menu.SelectInHost"), "Select in host shows while Live is on.");
        Assert.IsNull(ui.Host.TryFindInMenu(menu, "Menu.ResetView"), "Reset view shows outside temporary mode.");
    }

    [TestMethod]
    public void RowMenuListsActions()
    {
        using var ui = PanelUi.Open();
        ui.Collapse("All models");
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
        ui.Collapse("All models");
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
        ui.Collapse("All models");
        var menu = ui.OpenRowMenu("Ducts");

        var zoom = WpfSta.Invoke(() => menu.Items.OfType<MenuItem>().First(item => item.InputGestureText == "Z"));

        Assert.IsTrue(WpfSta.Invoke(() => zoom.ActualWidth > 0), "Zoom has no width.");
    }

    [TestMethod]
    public void RowHasNoMenuButton()
    {
        using var ui = PanelUi.Open();
        ui.Collapse("All models");
        var row = ui.Host.Row("Ducts");

        row.Patterns.SelectionItem.Pattern.Select();

        Assert.IsTrue(ThemeHost.WaitForUi(() => ui.Has(row, "Explorer.Row.Color")));
        Assert.IsFalse(ui.Has(row, "Explorer.Row.Menu"));
    }

    [TestMethod]
    public void IsolateToolRunsPlan()
    {
        using var ui = PanelUi.Open();
        ui.Collapse("All models");

        var row = ui.Host.Row("Ducts");
        row.Patterns.SelectionItem.Pattern.Select();
        ui.PressIn(row, "Explorer.Row.Isolate");

        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Model.ExecutedPlans.Any(plan => plan.Action == DevTools.ModelsContext.Model.VisibilityAction.Isolate)),
            "The isolate tool did not run a plan.");
    }

    [TestMethod]
    public void RowColorOpensPicker()
    {
        using var ui = PanelUi.Open();
        ui.Collapse("All models");
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

        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Model.ExecutedPlans.Any(plan => plan.Action == DevTools.ModelsContext.Model.VisibilityAction.Color)),
            "Confirming a color did not run a color plan.");
    }
    [TestMethod]
    public void ColoredRowShowsSwatch()
    {
        using var ui = PanelUi.Open();
        ui.Collapse("All models");
        WpfSta.Run(async () =>
        {
            var ducts = ui.Panel.Explorer.Rows.First(row => row.Text == "Ducts" && row.Kind == DevTools.ModelsContext.Model.TreeRowKind.Group);
            await ui.Panel.Explorer.RunToolAsync(ducts, MenuAction.Color);
            await ui.Panel.ContextMenu.ApplyColorCommand.ExecuteAsync(new DevTools.ModelsContext.Model.RgbColor(255, 0, 16));
        });

        ui.Host.Row("Walls").Patterns.SelectionItem.Pattern.Select();

        Assert.IsTrue(
            ThemeHost.WaitForUi(() => ui.Host.Row("Ducts").FindFirstDescendant(cf => cf.ByAutomationId("Explorer.Row.Color"))?.ItemStatus == "#FF0010"),
            "The colored row did not keep its swatch.");
        Assert.IsFalse(ui.Has(ui.Host.Row("Duct Fittings"), "Explorer.Row.Color"), "An uncolored row shows its color tool.");
    }

    [TestMethod]
    public void TicksShowAsTags()
    {
        using var ui = PanelUi.Open();
        var box = DocumentsBox(ui);
        Assert.IsNotNull(ui.Host.FindInWindow("ModelContext.Documents"));
        var keys = new[] { FakeModelGenerator.ArchFirst, FakeModelGenerator.ArchSecond, FakeModelGenerator.Mep };

        WpfSta.Invoke(() => ui.Panel.ContextBar.Tick(keys));

        Assert.IsTrue(ThemeHost.WaitFor(() => box.SelectedItems.Count == 4), "The box did not tick every document.");
        WpfSta.Invoke(() =>
        {
            box.UpdateLayout();
            var labels = ui.Panel.ContextBar.Documents.Where(item => item.IsChecked).Select(item => item.Label).ToList();
            var tags = Tags(box);
            CollectionAssert.AreEquivalent(labels, tags.Select(tag => tag.Content?.ToString()).ToList());
            foreach (var tag in tags)
            {
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
        var popup = OpenDocuments(ui);

        ToggleAll(ui, popup);

        Assert.IsTrue(ThemeHost.WaitFor(() => box.SelectedItems.Count == 3), "All did not tick only the loaded documents.");
        WpfSta.Invoke(() =>
        {
            var mep = ui.Panel.ContextBar.Documents.Single(item => item.Key == FakeModelGenerator.Mep);
            Assert.IsFalse(mep.IsChecked, "All ticked the unloaded link.");
            Assert.IsFalse(((System.Windows.UIElement)box.ItemContainerGenerator.ContainerFromItem(mep)).IsEnabled,
                "The unloaded link is not disabled.");
        });
    }

    [TestMethod]
    public void AllOffKeepsCurrentModel()
    {
        using var ui = PanelUi.Open();
        var box = DocumentsBox(ui);
        var popup = OpenDocuments(ui);
        ToggleAll(ui, popup);
        Assert.IsTrue(ThemeHost.WaitFor(() => box.SelectedItems.Count == 4), "All did not tick every document.");

        ToggleAll(ui, popup);

        Assert.IsTrue(ThemeHost.WaitFor(() => box.SelectedItems.Count == 1), "All off did not leave one document.");
        CollectionAssert.AreEqual(new[] { FakeModelGenerator.Current }, WpfSta.Invoke(() => ui.Panel.ContextBar.TickedDocuments.ToList()));
    }

    [TestMethod]
    public void LastTagCannotClose()
    {
        using var ui = PanelUi.Open();
        var box = DocumentsBox(ui);
        Assert.IsTrue(ThemeHost.WaitFor(() => Tags(box).Count == 1), "The current model has no tag.");

        CloseTag(box, ContextBarViewModel.CurrentModelLabel);

        Assert.AreEqual(1, WpfSta.Invoke(() => box.SelectedItems.Count), "Closing the last tag changed the selection.");
        CollectionAssert.AreEqual(new[] { FakeModelGenerator.Current }, WpfSta.Invoke(() => ui.Panel.ContextBar.TickedDocuments.ToList()));
        Assert.AreEqual(1, WpfSta.Invoke(() => Tags(box).Count));
    }

    [TestMethod]
    public void TagCloseUnticksLink()
    {
        using var ui = PanelUi.Open();
        var box = DocumentsBox(ui);
        WpfSta.Invoke(() => ui.Panel.ContextBar.Tick([FakeModelGenerator.Mep]));
        Assert.IsTrue(ThemeHost.WaitFor(() => Tags(box).Count == 2), "The link has no tag.");
        var label = WpfSta.Invoke(() => ui.Panel.ContextBar.LabelOf(FakeModelGenerator.Mep));

        CloseTag(box, label);

        Assert.IsTrue(ThemeHost.WaitFor(() => Tags(box).Count == 1), "The link tag did not close.");
        CollectionAssert.AreEqual(new[] { FakeModelGenerator.Current }, WpfSta.Invoke(() => ui.Panel.ContextBar.TickedDocuments.ToList()));
    }

    [TestMethod]
    public void TemporaryBarShowsAfterHide()
    {
        using var ui = PanelUi.Open();
        ui.Collapse("All models");
        Assert.IsNull(ui.Host.TryFindInWindow("Temporary.Keep"), "The temporary bar shows before any hide.");

        ui.PressIn(ui.Host.Row("Ducts"), "Explorer.Row.Eye");

        Assert.IsNotNull(ui.Host.FindInWindow("Temporary.Keep"));
        Assert.IsNotNull(ui.Host.FindInWindow("Temporary.Reset"));
    }

    [TestMethod]
    public void KeepAsksForLinkChanges()
    {
        using var ui = PanelUi.Open();
        WpfSta.Invoke(() => ui.Panel.ContextBar.Tick([FakeModelGenerator.Mep]));
        Assert.IsTrue(
            ThemeHost.WaitFor(() => ui.Panel.Explorer.Rows.Any(row => row.Text == "MEP-Mech.rvt")),
            "The link did not appear in Explorer.");
        ui.Collapse("All models");
        ui.Collapse("Current model");

        ui.PressIn(ui.Host.Row("Ducts"), "Explorer.Row.Eye");
        ui.Press("Temporary.Keep");

        Assert.IsNotNull(ui.Host.FindInWindow("Temporary.ConfirmKeep"));
        Assert.AreEqual(0, ui.Model.KeepCount);

        ui.Press("Temporary.ConfirmKeep");

        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Model.KeepCount == 1), "Keep did not run after the confirm.");
    }

    private static CheckComboBox DocumentsBox(PanelUi ui)
    {
        return WpfSta.Invoke(() => (CheckComboBox)ui.View.FindName("DocumentsBox"));
    }

    private static List<Tag> Tags(CheckComboBox box)
    {
        return ((Panel)box.Template.FindName("PART_Panel", box)).Children.OfType<Tag>().ToList();
    }

    private static Popup OpenDocuments(PanelUi ui)
    {
        var box = DocumentsBox(ui);
        var popup = WpfSta.Invoke(() =>
        {
            box.IsDropDownOpen = true;
            return (Popup)box.Template.FindName("PART_Popup", box);
        });
        Assert.IsTrue(ThemeHost.WaitFor(() => popup.IsOpen), "The documents drop-down did not open.");
        return popup;
    }

    private static void ToggleAll(PanelUi ui, Popup popup)
    {
        var all = ui.Host.FindInPopup(popup, "PART_SelectAll")
            .FindFirstDescendant(cf => cf.ByControlType(ControlType.CheckBox));
        Assert.IsNotNull(all, "The All item has no check box.");
        all.Patterns.Toggle.Pattern.Toggle();
    }

    private static void CloseTag(CheckComboBox box, string label)
    {
        WpfSta.Invoke(() =>
        {
            box.UpdateLayout();
            var tag = Tags(box).Single(tag => tag.Content?.ToString() == label);
            var close = (Button)tag.Template.FindName("ButtonClose", tag);
            var peer = new ButtonAutomationPeer(close);
            ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();
        });
        WpfSta.Current.Invoke(() => { }, DispatcherPriority.ContextIdle);
    }
}
