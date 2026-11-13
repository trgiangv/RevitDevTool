using DevTools.UI.Controls;
using global::HandyControl.Controls;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace DevTools.HandyControl.Tests;

[TestClass]
[DoNotParallelize]
public sealed class SummaryCheckComboBoxTests
{
    private const string BoxId = "DocumentsBox";

    [TestMethod]
    public void AllTracksManualTicks()
    {
        var box = CreateBox(3);
        using var host = ThemeHost.Show(box);
        Expand(host);

        for (var index = 0; index < 3; index++)
        {
            Tick(host, ItemId(index));
        }

        Assert.IsTrue(ThemeHost.WaitFor(() => AllItem(box).IsSelected), "All did not tick after every item was ticked.");

        Tick(host, ItemId(1));

        Assert.IsTrue(ThemeHost.WaitFor(() => !AllItem(box).IsSelected), "All stayed ticked after an item was unticked.");
    }

    [TestMethod]
    public void AllSkipsDisabledItems()
    {
        var box = CreateBox(3);
        WpfSta.Invoke(() => ((UIElement)box.Items[2]).IsEnabled = false);
        using var host = ThemeHost.Show(box);
        Expand(host);

        var all = host.FindInPopup(WpfSta.Invoke(() => PopupOf(box)), "SummaryCheckComboBoxAll");
        Toggle(all);

        Assert.IsTrue(ThemeHost.WaitFor(() => box.SelectedItems.Count == 2), "All did not tick only the enabled items. ticked=" + WpfSta.Invoke(() => box.SelectedItems.Count));
        WpfSta.Invoke(() =>
        {
            Assert.IsFalse(box.SelectedItems.Contains(box.Items[2]), "All ticked a disabled item.");
            Assert.IsTrue(AllItem(box).IsSelected, "All did not stay ticked.");
        });
    }

    [TestMethod]
    public void SummaryShowsOneLine()
    {
        var box = CreateBox(30);
        using var host = ThemeHost.Show(box);
        WpfSta.Invoke(() =>
        {
            foreach (var item in box.Items)
            {
                box.SelectedItems.Add(item);
            }

            box.UpdateLayout();
        });

        var expected = string.Join(SummaryCheckComboBox.SummarySeparator, Enumerable.Range(0, 30).Select(Name));
        Assert.AreEqual(expected, host.FindInWindow(BoxId).Patterns.Value.Pattern.Value.Value);
        WpfSta.Invoke(() =>
        {
            var text = (TextBlock)box.Template.FindName("PART_Summary", box);
            Assert.IsNull(box.Template.FindName("PART_Panel", box), "Summary template still has a tag panel.");
            Assert.AreEqual(TextWrapping.NoWrap, text.TextWrapping);
            Assert.AreEqual(TextTrimming.CharacterEllipsis, text.TextTrimming);
            Assert.AreEqual(expected, text.ToolTip);
            Assert.IsTrue(text.ActualHeight < text.FontSize * 2, "Summary wraps past one line. height=" + text.ActualHeight);
        });
    }

    [TestMethod]
    public void SummaryHeightStaysFixed()
    {
        var box = CreateBox(30);
        using var host = ThemeHost.Show(box);
        var heights = WpfSta.Invoke(() =>
        {
            box.SelectedItems.Add(box.Items[0]);
            box.UpdateLayout();
            var one = box.ActualHeight;
            foreach (var item in box.Items)
            {
                box.SelectedItems.Add(item);
            }

            box.UpdateLayout();
            return (one, many: box.ActualHeight);
        });

        Assert.AreEqual(28d, heights.one);
        Assert.AreEqual(heights.one, heights.many);
    }

    private static SummaryCheckComboBox CreateBox(int count)
    {
        return WpfSta.Invoke(() =>
        {
            var box = new SummaryCheckComboBox
            {
                Width = 240,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            AutomationProperties.SetAutomationId(box, BoxId);
            for (var index = 0; index < count; index++)
            {
                var item = new CheckComboBoxItem { Content = Name(index) };
                AutomationProperties.SetAutomationId(item, ItemId(index));
                box.Items.Add(item);
            }

            return box;
        });
    }

    private static void Expand(ThemeHost host)
    {
        host.FindInWindow(BoxId).Patterns.ExpandCollapse.Pattern.Expand();
        Assert.IsTrue(ThemeHost.WaitFor(() => host.Window.Content is SummaryCheckComboBox { IsDropDownOpen: true } box && PopupOf(box).IsOpen), "Drop-down did not open.");
    }

    private static void Tick(ThemeHost host, string automationId)
    {
        Toggle(host.FindInWindow(automationId));
    }

    private static void Toggle(FlaUI.Core.AutomationElements.AutomationElement element)
    {
        var check = element.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.CheckBox));
        Assert.IsNotNull(check, element.AutomationId + " has no check box.");
        check.Patterns.Toggle.Pattern.Toggle();
    }

    private static string Name(int index)
    {
        return "Linked model number " + index;
    }

    private static string ItemId(int index)
    {
        return "DocumentItem" + index;
    }

    private static Popup PopupOf(SummaryCheckComboBox box)
    {
        return (Popup)box.Template.FindName("PART_Popup", box);
    }

    private static CheckComboBoxItem AllItem(SummaryCheckComboBox box)
    {
        return (CheckComboBoxItem)box.Template.FindName("PART_SelectAll", box);
    }
}
