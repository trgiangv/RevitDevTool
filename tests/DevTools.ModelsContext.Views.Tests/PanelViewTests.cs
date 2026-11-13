using DevTools.ModelsContext.Tests.Shared;
using FlaUI.Core.Definitions;
using System.Windows.Controls;

namespace DevTools.ModelsContext.Views.Tests;

[TestClass]
[DoNotParallelize]
public sealed class PanelViewTests
{
    [TestMethod]
    public void TabsAreTreeAndFilter()
    {
        using var ui = PanelUi.Open();

        var tabs = ui.Host.FindInWindow("ModelContext.Tabs")
            .FindAllDescendants(cf => cf.ByControlType(ControlType.TabItem))
            .Select(tab => tab.Name)
            .ToArray();

        CollectionAssert.AreEqual(new[] { "Tree", "Filter" }, tabs);
    }

    [TestMethod]
    public void GroupHeaderHasNoControls()
    {
        using var ui = PanelUi.Open();
        ui.Collapse("All models");

        var header = ui.Host.Row("Current model");
        var group = ui.Host.Row("Ducts");

        foreach (var id in new[] { "Tree.Row.Eye", "Tree.Row.Isolate", "Tree.Row.Halftone", "Tree.Row.Color" })
        {
            Assert.IsFalse(ui.Has(header, id), "Header row has " + id);
        }

        Assert.IsTrue(ui.Has(group, "Tree.Row.Eye"), "Group row has no eye.");
    }

    [TestMethod]
    public void EyeTogglesRow()
    {
        using var ui = PanelUi.Open();
        ui.Collapse("All models");
        Assert.AreEqual("Shown", ui.Host.Row("Ducts").FindFirstDescendant(cf => cf.ByAutomationId("Tree.Row.Eye"))?.ItemStatus);

        ui.PressIn(ui.Host.Row("Ducts"), "Tree.Row.Eye");

        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Model.ExecutedPlans.Count == 1), "The eye did not run a plan.");
        Assert.IsTrue(
            ThemeHost.WaitForUi(() => ui.Host.Row("Ducts").FindFirstDescendant(cf => cf.ByAutomationId("Tree.Row.Eye"))?.ItemStatus == "Hidden"),
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

        Assert.IsNull(ui.Host.TryFindInMenu(menu, "Menu.SelectInRevit"), "Select in Revit shows while Live is on.");
        Assert.IsNull(ui.Host.TryFindInMenu(menu, "Menu.ResetView"), "Reset view shows outside temporary mode.");
    }

    [TestMethod]
    public void DocumentsBoxShowsOneLine()
    {
        using var ui = PanelUi.Open();
        var keys = new[] { FakeModelGenerator.ArchFirst, FakeModelGenerator.ArchSecond, FakeModelGenerator.Mep };
        WpfSta.Invoke(() => ui.Panel.ContextBar.Tick(keys));
        var box = WpfSta.Invoke(() => (DevTools.UI.Controls.SummaryCheckComboBox)ui.View.FindName("DocumentsBox"));
        Assert.IsTrue(ThemeHost.WaitFor(() => box.SelectedItems.Count == 4), "The box did not tick every document.");

        var expected = WpfSta.Invoke(() => ui.Panel.ContextBar.Summary);
        Assert.AreEqual(expected, ui.Host.FindInWindow("ModelContext.Documents").Patterns.Value.Pattern.Value.Value);
        WpfSta.Invoke(() =>
        {
            box.UpdateLayout();
            var text = (TextBlock)box.Template.FindName("PART_Summary", box);
            Assert.AreEqual(System.Windows.TextWrapping.NoWrap, text.TextWrapping);
            Assert.AreEqual(System.Windows.TextTrimming.CharacterEllipsis, text.TextTrimming);
            Assert.IsTrue(text.ActualHeight < text.FontSize * 2, "Summary wraps past one line. height=" + text.ActualHeight);
        });
    }

    [TestMethod]
    public void TemporaryBarShowsAfterHide()
    {
        using var ui = PanelUi.Open();
        ui.Collapse("All models");
        Assert.IsNull(ui.Host.TryFindInWindow("Temporary.Keep"), "The temporary bar shows before any hide.");

        ui.PressIn(ui.Host.Row("Ducts"), "Tree.Row.Eye");

        Assert.IsNotNull(ui.Host.FindInWindow("Temporary.Keep"));
        Assert.IsNotNull(ui.Host.FindInWindow("Temporary.Reset"));
    }

    [TestMethod]
    public void KeepAsksForLinkChanges()
    {
        using var ui = PanelUi.Open();
        WpfSta.Invoke(() => ui.Panel.ContextBar.Tick([FakeModelGenerator.Mep]));
        Assert.IsTrue(
            ThemeHost.WaitFor(() => ui.Panel.Tree.Rows.Any(row => row.Text == "MEP-Mech.rvt")),
            "The link did not appear in the tree.");
        ui.Collapse("All models");
        ui.Collapse("Current model");

        ui.PressIn(ui.Host.Row("Ducts"), "Tree.Row.Eye");
        ui.Press("Temporary.Keep");

        Assert.IsNotNull(ui.Host.FindInWindow("Temporary.ConfirmKeep"));
        Assert.AreEqual(0, ui.Model.KeepCount);

        ui.Press("Temporary.ConfirmKeep");

        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Model.KeepCount == 1), "Keep did not run after the confirm.");
    }
}
