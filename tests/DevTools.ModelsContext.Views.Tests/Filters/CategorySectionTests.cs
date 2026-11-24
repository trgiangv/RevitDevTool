using DevTools.ModelsContext.Filters;
using DevTools.Presentation.Views.ModelsContext.Filters;
using System.Windows.Controls;
using System.Windows.Media;

namespace DevTools.ModelsContext.Views.Tests.Filters;

[TestClass]
[DoNotParallelize]
public sealed class CategorySectionTests
{
    [TestMethod]
    [DataRow("Explorer")]
    [DataRow("Filter")]
    public void SelectTicksOnlyRow(string prefix)
    {
        using var ui = OpenBox(prefix);

        Rows(ui, prefix)[1].Patterns.SelectionItem.Pattern.Select();

        Assert.IsTrue(ThemeHost.WaitFor(() => Ticked(ui, prefix).SequenceEqual([1])), "Select did not tick only that row.");
    }

    [TestMethod]
    [DataRow("Explorer")]
    [DataRow("Filter")]
    public void AddKeepsBothTicked(string prefix)
    {
        using var ui = OpenBox(prefix);
        var rows = Rows(ui, prefix);

        rows[0].Patterns.SelectionItem.Pattern.Select();
        rows[2].Patterns.SelectionItem.Pattern.AddToSelection();

        Assert.IsTrue(ThemeHost.WaitFor(() => Ticked(ui, prefix).SequenceEqual([0, 2])), "Add did not keep both ticked.");
    }

    [TestMethod]
    [DataRow("Explorer")]
    [DataRow("Filter")]
    public void RemoveUnticksRow(string prefix)
    {
        using var ui = OpenBox(prefix);
        var rows = Rows(ui, prefix);
        rows[0].Patterns.SelectionItem.Pattern.Select();
        rows[2].Patterns.SelectionItem.Pattern.AddToSelection();

        rows[0].Patterns.SelectionItem.Pattern.RemoveFromSelection();

        Assert.IsTrue(ThemeHost.WaitFor(() => Ticked(ui, prefix).SequenceEqual([2])), "Remove did not untick the row.");
    }

    [TestMethod]
    [DataRow("Explorer")]
    [DataRow("Filter")]
    public void BoxTickKeepsOthers(string prefix)
    {
        using var ui = OpenBox(prefix);
        Rows(ui, prefix)[0].Patterns.SelectionItem.Pattern.Select();
        Assert.IsTrue(ThemeHost.WaitFor(() => Ticked(ui, prefix).SequenceEqual([0])), "Select did not tick the row.");

        ui.Host.FindAllInWindow(prefix + ".Category.Item")[3].Patterns.Toggle.Pattern.Toggle();

        Assert.IsTrue(ThemeHost.WaitFor(() => Ticked(ui, prefix).SequenceEqual([0, 3])), "The box did not keep the other tick.");
        Assert.IsTrue(WpfSta.Invoke(() => Container(ui, prefix, 3).IsSelected), "The box tick did not select its row.");
    }

    [TestMethod]
    [DataRow("Explorer")]
    [DataRow("Filter")]
    public void SelectedRowIsPlain(string prefix)
    {
        using var ui = OpenBox(prefix);

        Rows(ui, prefix)[0].Patterns.SelectionItem.Pattern.Select();

        Assert.IsTrue(ThemeHost.WaitForUi(() => WpfSta.Invoke(() =>
        {
            var row = Container(ui, prefix, 0);
            return row.IsSelected && row.Background is SolidColorBrush { Color.A: 0 };
        })), "A selected row is painted.");
    }

    [TestMethod]
    [DataRow("Explorer")]
    [DataRow("Filter")]
    public void AllSelectsEveryRow(string prefix)
    {
        using var ui = OpenBox(prefix);

        ui.Press(prefix + ".CategoryAll");

        Assert.IsTrue(ThemeHost.WaitForUi(() => WpfSta.Invoke(() =>
        {
            var list = List(ui, prefix);
            return list.Items.Count > 8 && list.SelectedItems.Count == list.Items.Count
                && Enumerable.Range(0, list.Items.Count).All(index => Container(ui, prefix, index).IsSelected);
        })), "All did not select every row.");
    }

    private static PanelUi OpenBox(string prefix)
    {
        var ui = PanelUi.Open();
        if (prefix == "Filter")
        {
            ui.ShowView("ModelContext.View.Filter");
            ui.Press("Filter.Add");
            Assert.IsTrue(ThemeHost.WaitFor(() => ui.Panel.Filter.Library.SelectedSource is { IsCustom: true }),
                "No custom filter was added.");
        }

        ui.Host.FindInWindow(prefix + ".Categories").Patterns.Toggle.Pattern.Toggle();
        ui.Press(prefix + ".CategoryNone");
        Assert.IsTrue(ThemeHost.WaitFor(() => Ticked(ui, prefix).Length == 0), "None left ticks.");
        Assert.IsTrue(ThemeHost.WaitForUi(() => Rows(ui, prefix).Length > 3), "The box lists too few rows.");
        return ui;
    }

    private static FlaUI.Core.AutomationElements.AutomationElement[] Rows(PanelUi ui, string prefix)
    {
        return ui.Host.FindAllInWindow(prefix + ".Category.Row");
    }

    private static int[] Ticked(PanelUi ui, string prefix)
    {
        return WpfSta.Invoke(() => List(ui, prefix).Items.Cast<CategoryItemViewModel>()
            .Select((item, index) => (item, index)).Where(pair => pair.item.IsChecked).Select(pair => pair.index).ToArray());
    }

    private static ListBoxItem Container(PanelUi ui, string prefix, int index)
    {
        return (ListBoxItem)List(ui, prefix).ItemContainerGenerator.ContainerFromIndex(index);
    }

    private static ListBox List(PanelUi ui, string prefix)
    {
        var tab = (System.Windows.FrameworkElement)ui.View.FindName(prefix + "View");
        var section = (CategorySection)tab.FindName("CategorySection");
        return (ListBox)section.FindName("CategoryList");
    }
}
