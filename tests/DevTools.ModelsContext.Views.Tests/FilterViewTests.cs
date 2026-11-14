using DevTools.ModelsContext.ViewModels;
using FlaUI.Core.Definitions;

namespace DevTools.ModelsContext.Views.Tests;

[TestClass]
[DoNotParallelize]
public sealed class FilterViewTests
{
    [TestMethod]
    public void AllTicksListedCategories()
    {
        using var ui = OpenFilter();
        ui.Host.FindInWindow("Filter.Categories").Patterns.Toggle.Pattern.Toggle();

        ui.Press("Filter.CategoryAll");

        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Panel.Filter.Categories.All(category => category.IsChecked)),
            "All did not tick every category.");
    }

    [TestMethod]
    public void CategoryRowsHaveNoSelection()
    {
        using var ui = OpenFilter();
        ui.Host.FindInWindow("Filter.Categories").Patterns.Toggle.Pattern.Toggle();
        var list = ui.Host.FindInWindow("Filter.CategoryList");
        var row = list.FindFirstDescendant(cf => cf.ByControlType(ControlType.ListItem));

        Assert.IsNotNull(row, "The category list has no rows.");

        row.Patterns.SelectionItem.Pattern.Select();

        var clear = WpfSta.Invoke(() =>
        {
            var box = (System.Windows.Controls.ListBox)FindByAutomationId(ui, "Filter.CategoryList");
            var item = (System.Windows.Controls.ListBoxItem)box.ItemContainerGenerator.ContainerFromIndex(0);
            return item.IsSelected && item.Background is System.Windows.Media.SolidColorBrush { Color.A: 0 };
        });
        Assert.IsTrue(clear, "A selected category row is painted.");
    }

    [TestMethod]
    public void EmptyRulesShowHint()
    {
        using var ui = OpenFilter();
        Assert.IsNotNull(ui.Host.FindInWindow("Filter.NoRules"));

        ui.Press("Filter.Rule.Add");

        Assert.IsTrue(ThemeHost.WaitForUi(() => ui.Host.TryFindInWindow("Filter.NoRules") is null),
            "The empty hint stayed after adding a rule.");
    }

    [TestMethod]
    public void ReadOnlyHidesAddButtons()
    {
        using var ui = PanelUi.Open();
        ui.ShowTab("ModelContext.Tab.Filter");
        WpfSta.Invoke(() => ui.Panel.Filter.SelectedSource =
            ui.Panel.Filter.Sources.First(source => source.Kind == FilterSourceKind.RuleBased));

        Assert.IsTrue(ThemeHost.WaitFor(() =>
            ui.Panel.Filter.IsReadOnly && ui.Panel.Filter.SelectedSource?.Kind == FilterSourceKind.RuleBased), "The Revit filter did not open read only.");
        Assert.IsTrue(ThemeHost.WaitForUi(() => ui.Host.TryFindInWindow("Filter.Rule.Add") is null),
            "+ Rule shows for a read-only filter.");
    }

    [TestMethod]
    public void ResultShowsMatchCount()
    {
        using var ui = OpenFilter();
        WpfSta.Invoke(() =>
        {
            ui.Panel.Filter.Categories[0].IsChecked = true;
            ui.Clock.Advance(FilterTabViewModel.LiveDelay);
        });
        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Panel.Filter.ResultCount > 0), "The filter found nothing.");

        var expected = WpfSta.Invoke(() => ui.Panel.Filter.ResultCount).ToString(System.Globalization.CultureInfo.InvariantCulture);

        Assert.IsTrue(ThemeHost.WaitForUi(() => Digits(ui.Host.FindInWindow("Filter.ResultCount").Name) == expected),
            "The count shows " + ui.Host.FindInWindow("Filter.ResultCount").Name + ", expected " + expected);
    }

    private static string Digits(string text)
    {
        return new string(text.Where(char.IsDigit).ToArray());
    }

    private static PanelUi OpenFilter()
    {
        var ui = PanelUi.Open();
        ui.ShowTab("ModelContext.Tab.Filter");
        ui.Press("Filter.Add");
        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Panel.Filter.SelectedSource is { IsCustom: true }),
            "No custom filter was added.");
        return ui;
    }

    private static System.Windows.FrameworkElement FindByAutomationId(PanelUi ui, string automationId)
    {
        var queue = new Queue<System.Windows.DependencyObject>();
        queue.Enqueue(ui.View);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current is System.Windows.FrameworkElement element
                && System.Windows.Automation.AutomationProperties.GetAutomationId(element) == automationId)
            {
                return element;
            }

            for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(current); index++)
            {
                queue.Enqueue(System.Windows.Media.VisualTreeHelper.GetChild(current, index));
            }
        }

        throw new InvalidOperationException(automationId + " was not found.");
    }
}
