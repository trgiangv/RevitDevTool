using DevTools.UI.Controls;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace DevTools.HandyControl.Tests;

[TestClass]
[DoNotParallelize]
public sealed class FlatTreeListTests
{
    [TestMethod]
    public void FlatListRealizesFewContainers()
    {
        var list = CreateList(100_000);
        using var host = ThemeHost.Show(HostOf(list), height: 600);

        var realized = WpfSta.Invoke(() =>
        {
            list.UpdateLayout();
            return CountContainers(list);
        });

        Assert.IsTrue(realized is > 0 and < 100, "Realized containers: " + realized);
    }

    [TestMethod]
    public void FlatListRowIsTwentyEight()
    {
        var list = CreateList(10);
        using var host = ThemeHost.Show(HostOf(list), height: 600);

        WpfSta.Invoke(() =>
        {
            list.UpdateLayout();
            var first = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0);
            var second = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(1);
            Assert.AreEqual(28d, first.ActualHeight);
            Assert.AreEqual(28d, second.TranslatePoint(new Point(), first).Y);
            Assert.AreEqual(SelectionMode.Extended, list.SelectionMode);
            Assert.AreEqual(VirtualizationMode.Recycling, VirtualizingPanel.GetVirtualizationMode(list));
            Assert.AreEqual(ScrollUnit.Item, VirtualizingPanel.GetScrollUnit(list));
        });
    }

    private static FlatTreeList CreateList(int count)
    {
        return WpfSta.Invoke(() =>
        {
            var list = new FlatTreeList { ItemsSource = Enumerable.Range(0, count).Select(index => "Row " + index).ToList() };
            AutomationProperties.SetAutomationId(list, "FlatList");
            return list;
        });
    }

    private static Grid HostOf(FlatTreeList list)
    {
        return WpfSta.Invoke(() =>
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.Children.Add(list);
            return grid;
        });
    }

    private static int CountContainers(DependencyObject node)
    {
        var count = node is ListBoxItem ? 1 : 0;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(node); index++)
        {
            count += CountContainers(VisualTreeHelper.GetChild(node, index));
        }

        return count;
    }
}
