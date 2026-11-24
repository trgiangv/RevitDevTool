using DevTools.UI.Controls;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using AutomationElement = FlaUI.Core.AutomationElements.AutomationElement;

namespace DevTools.HandyControl.Tests;

[TestClass]
[DoNotParallelize]
public sealed class TagCheckComboBoxTests
{
    private const string BoxId = "Docs";

    private static readonly Lazy<ResourceDictionary> Resources = new(() => WpfSta.Invoke(() => new ResourceDictionary
    {
        Source = new Uri("/DevTools.UI;component/Controls/TagCheckComboBox/TagCheckComboBox.xaml", UriKind.Relative),
    }));

    [TestMethod]
    public void OverflowShowsPlusCount()
    {
        var (box, docs) = CreateBox(10);
        using var host = ThemeHost.Show(box);
        SelectAll(box, docs);

        var hidden = WpfSta.Invoke(() => box.HiddenTagCount);
        Assert.IsTrue(hidden is > 0 and < 10, "Hidden tag count: " + hidden);

        var more = host.FindInWindow(BoxId + "." + TagCheckComboBox.MoreIdSuffix);
        Assert.AreEqual("+" + hidden, more.Name);
        WpfSta.Invoke(() =>
        {
            var overflow = Overflow(box);
            StringAssert.Contains((string)overflow.ToolTip, docs[9].Label);
            Assert.IsTrue(LayoutInformation.GetLayoutSlot(overflow).Width > 0, "+N tag is not shown.");
            Assert.AreEqual(28d, box.ActualHeight);
        });
    }

    [TestMethod]
    public void AllTagsFitWithoutPlus()
    {
        var (box, docs) = CreateBox(2, width: 600);
        using var host = ThemeHost.Show(box);
        SelectAll(box, docs);

        WpfSta.Invoke(() =>
        {
            Assert.AreEqual(0, box.HiddenTagCount);
            Assert.AreEqual(0d, LayoutInformation.GetLayoutSlot(Overflow(box)).Width);
        });
    }

    [TestMethod]
    public void UiaListsOnlyShownTags()
    {
        var (box, docs) = CreateBox(10);
        using var host = ThemeHost.Show(box);
        SelectAll(box, docs);
        var shown = WpfSta.Invoke(() => 10 - box.HiddenTagCount);

        var tags = host.FindInWindow(BoxId).FindAllChildren().Count(c => c.Properties.AutomationId.ValueOrDefault == BoxId + ".Tag");

        Assert.AreEqual(shown, tags);
    }

    [TestMethod]
    public void UiaHidesZeroPlusTag()
    {
        var (box, docs) = CreateBox(2, width: 600);
        using var host = ThemeHost.Show(box);
        SelectAll(box, docs);

        var more = host.FindInWindow(BoxId).FindAllChildren().Count(c => c.Properties.AutomationId.ValueOrDefault == BoxId + "." + TagCheckComboBox.MoreIdSuffix);

        Assert.AreEqual(0, more);
    }

    [TestMethod]
    public void OverflowGrowsOnShrink()
    {
        var (box, docs) = CreateBox(3, width: 900);
        using var host = ThemeHost.Show(box);
        SelectAll(box, docs);
        Assert.AreEqual(0, WpfSta.Invoke(() => box.HiddenTagCount));

        WpfSta.Invoke(() =>
        {
            box.Width = 200;
            box.UpdateLayout();
        });

        Assert.IsTrue(WpfSta.Invoke(() => box.HiddenTagCount) > 0, "Shrinking did not hide tags.");
    }

    [TestMethod]
    public void NarrowBoxKeepsFirstTag()
    {
        var (box, docs) = CreateBox(3, width: 90);
        using var host = ThemeHost.Show(box);
        SelectAll(box, docs);

        Assert.AreEqual(2, WpfSta.Invoke(() => box.HiddenTagCount));
        var tags = host.FindInWindow(BoxId).FindAllChildren().Count(c => c.Properties.AutomationId.ValueOrDefault == BoxId + ".Tag");
        Assert.AreEqual(1, tags);
    }

    [TestMethod]
    public void TagRemoveDeselectsItem()
    {
        var (box, docs) = CreateBox(3, width: 600);
        using var host = ThemeHost.Show(box);
        SelectAll(box, docs);

        var remove = FindTagRemove(host, docs[0].Label);
        remove.Patterns.Invoke.Pattern.Invoke();

        Assert.IsTrue(ThemeHost.WaitFor(() => !box.SelectedItems.Contains(docs[0]) && box.SelectedItems.Count == 2), "Tag remove did not deselect the item.");
    }

    [TestMethod]
    public void SearchFiltersItems()
    {
        var (box, docs) = CreateBox(12);
        using var host = ThemeHost.Show(box);
        Expand(host, box);

        host.FindInWindow(BoxId + "." + TagCheckComboBox.SearchIdSuffix).Patterns.Value.Pattern.SetValue("NUMBER 1");

        Assert.IsTrue(ThemeHost.WaitFor(() => box.SearchText == "NUMBER 1"), "Search text did not reach the box.");
        WpfSta.Invoke(() =>
        {
            box.UpdateLayout();
            var shown = VisibleLabels(box);
            CollectionAssert.AreEqual(new[] { docs[1].Label, docs[10].Label, docs[11].Label }, shown);
        });
    }

    [TestMethod]
    public void SearchClearButtonEmptiesText()
    {
        var (box, _) = CreateBox(5);
        using var host = ThemeHost.Show(box);
        Expand(host, box);
        host.FindInWindow(BoxId + "." + TagCheckComboBox.SearchIdSuffix).Patterns.Value.Pattern.SetValue("number 3");
        Assert.IsTrue(ThemeHost.WaitFor(() => box.SearchText == "number 3"), "Search text did not reach the box.");

        WpfSta.Invoke(() =>
        {
            var search = (global::HandyControl.Controls.SearchBar)box.Template.FindName("PART_Search", box);
            var clear = (Button)search.Template.FindName("ButtonClear", search);
            Assert.IsTrue(clear.IsEnabled, "The clear button has no handler.");
            ((System.Windows.Input.RoutedCommand)clear.Command).Execute(clear.CommandParameter, clear);
        });

        Assert.IsTrue(ThemeHost.WaitFor(() => box.SearchText.Length == 0), "The clear button left the search text.");
        Assert.IsTrue(WpfSta.Invoke(() => box.IsDropDownOpen && PopupOf(box).IsOpen), "Clearing closed the drop-down.");
    }

    [TestMethod]
    public void SearchUsesStockSearchBar()
    {
        var (box, _) = CreateBox(5);
        using var host = ThemeHost.Show(box);
        Expand(host, box);

        var search = host.FindInWindow(BoxId + "." + TagCheckComboBox.SearchIdSuffix);

        Assert.AreEqual(nameof(global::HandyControl.Controls.SearchBar), WpfSta.Invoke(() => box.Template.FindName("PART_Search", box).GetType().Name));
        Assert.IsTrue(search.Patterns.Value.IsSupported, "The search box has no Value pattern.");
    }

    [TestMethod]
    public void SearchShowsNoMatches()
    {
        var (box, _) = CreateBox(5);
        using var host = ThemeHost.Show(box);
        Expand(host, box);

        WpfSta.Invoke(() => box.SearchText = "zzz");

        WpfSta.Invoke(() =>
        {
            box.UpdateLayout();
            Assert.IsFalse(box.HasFilterMatches);
            Assert.AreEqual(Visibility.Visible, ((FrameworkElement)box.Template.FindName("PART_Empty", box)).Visibility);
        });
    }

    [TestMethod]
    public void SearchClearsOnClose()
    {
        var (box, _) = CreateBox(5);
        using var host = ThemeHost.Show(box);
        Expand(host, box);
        WpfSta.Invoke(() => box.SearchText = "number 3");

        host.FindInWindow(BoxId).Patterns.ExpandCollapse.Pattern.Collapse();

        Assert.IsTrue(ThemeHost.WaitFor(() => !box.IsDropDownOpen && box.SearchText.Length == 0), "Search text stayed after close.");
    }

    [TestMethod]
    public void SelectAllRespectsFilter()
    {
        var (box, docs) = CreateBox(12);
        using var host = ThemeHost.Show(box);
        Expand(host, box);
        WpfSta.Invoke(() => box.SearchText = "number 1");

        host.FindInWindow(BoxId + "." + TagCheckComboBox.SelectAllIdSuffix).Patterns.Toggle.Pattern.Toggle();

        Assert.IsTrue(ThemeHost.WaitFor(() => box.SelectedItems.Count == 3), "Select all did not follow the filter. selected=" + WpfSta.Invoke(() => box.SelectedItems.Count));
        WpfSta.Invoke(() => CollectionAssert.AreEquivalent(new[] { docs[1], docs[10], docs[11] }, box.SelectedItems.Cast<Doc>().ToList()));
    }

    [TestMethod]
    public void DisabledItemStaysUnchecked()
    {
        var (box, docs) = CreateBox(4, disabled: 2);
        using var host = ThemeHost.Show(box);

        var item = host.FindInWindow(ItemId(2));
        var refused = false;
        try
        {
            item.Patterns.Toggle.Pattern.Toggle();
        }
        catch (Exception)
        {
            refused = true;
        }

        Assert.IsTrue(refused, "UIA toggled a disabled item.");
        WpfSta.Invoke(() => box.SetFilteredSelection(true));

        WpfSta.Invoke(() =>
        {
            Assert.AreEqual(3, box.SelectedItems.Count);
            Assert.IsFalse(box.SelectedItems.Contains(docs[2]), "A disabled item was checked.");
        });
    }

    [TestMethod]
    public void UiaToggleWhileClosed()
    {
        var (box, docs) = CreateBox(30);
        using var host = ThemeHost.Show(box);

        var item = host.FindInWindow(ItemId(25));
        item.Patterns.Toggle.Pattern.Toggle();

        Assert.IsTrue(ThemeHost.WaitFor(() => box.SelectedItems.Contains(docs[25])), "UIA toggle did not select the item.");
        Assert.IsFalse(WpfSta.Invoke(() => box.IsDropDownOpen), "Toggle opened the drop-down.");

        item.Patterns.SelectionItem.Pattern.RemoveFromSelection();

        Assert.IsTrue(ThemeHost.WaitFor(() => box.SelectedItems.Count == 0), "UIA remove did not deselect the item.");
    }

    [TestMethod]
    public void UiaToggleAfterDropDownCloses()
    {
        var (box, docs) = CreateBox(5);
        using var host = ThemeHost.Show(box);
        Expand(host, box);
        host.FindInWindow(BoxId).Patterns.ExpandCollapse.Pattern.Collapse();
        Assert.IsTrue(ThemeHost.WaitFor(() => !box.IsDropDownOpen), "Drop-down did not close.");

        host.FindInWindow(ItemId(2)).Patterns.Toggle.Pattern.Toggle();

        Assert.IsTrue(ThemeHost.WaitFor(() => box.SelectedItems.Contains(docs[2])), "UIA toggle after close did not select the item.");
    }

    [TestMethod]
    public void TickKeepsDropDownOpen()
    {
        var (box, _) = CreateBox(5);
        using var host = ThemeHost.Show(box);
        Expand(host, box);

        host.FindInWindow(ItemId(1)).Patterns.Toggle.Pattern.Toggle();
        host.FindInWindow(ItemId(3)).Patterns.Toggle.Pattern.Toggle();

        Assert.IsTrue(ThemeHost.WaitFor(() => box.SelectedItems.Count == 2), "Ticking did not select both items.");
        Assert.IsTrue(WpfSta.Invoke(() => box.IsDropDownOpen && PopupOf(box).IsOpen), "Ticking closed the drop-down.");
    }

    [TestMethod]
    public void SelectAllKeepsDropDownOpen()
    {
        var (box, _) = CreateBox(5);
        using var host = ThemeHost.Show(box);
        Expand(host, box);

        host.FindInWindow(BoxId + "." + TagCheckComboBox.SelectAllIdSuffix).Patterns.Toggle.Pattern.Toggle();

        Assert.IsTrue(ThemeHost.WaitFor(() => box.SelectedItems.Count == 5), "Select all did not select every item.");
        Assert.IsTrue(WpfSta.Invoke(() => box.IsDropDownOpen && PopupOf(box).IsOpen), "Select all closed the drop-down.");
    }

    [TestMethod]
    public void TagRemoveKeepsDropDownClosed()
    {
        var (box, docs) = CreateBox(3, width: 600);
        using var host = ThemeHost.Show(box);
        SelectAll(box, docs);

        FindTagRemove(host, docs[1].Label).Patterns.Invoke.Pattern.Invoke();

        Assert.IsTrue(ThemeHost.WaitFor(() => box.SelectedItems.Count == 2), "Tag remove did not deselect the item.");
        Assert.IsFalse(WpfSta.Invoke(() => box.IsDropDownOpen || PopupOf(box).IsOpen), "Tag remove opened the drop-down.");
    }

    [TestMethod]
    public void TagRemoveKeepsDropDownOpen()
    {
        var (box, docs) = CreateBox(3, width: 600);
        using var host = ThemeHost.Show(box);
        SelectAll(box, docs);
        Expand(host, box);

        FindTagRemove(host, docs[1].Label).Patterns.Invoke.Pattern.Invoke();

        Assert.IsTrue(ThemeHost.WaitFor(() => box.SelectedItems.Count == 2), "Tag remove did not deselect the item.");
        Assert.IsTrue(WpfSta.Invoke(() => box.IsDropDownOpen && PopupOf(box).IsOpen), "Tag remove closed the drop-down.");
    }

    [TestMethod]
    public void ItemRowsUseStockCheckBox()
    {
        var (box, docs) = CreateBox(3);
        using var host = ThemeHost.Show(box);
        Expand(host, box);
        host.FindInWindow(ItemId(0)).Patterns.Toggle.Pattern.Toggle();

        Assert.IsTrue(ThemeHost.WaitFor(() => box.SelectedItems.Contains(docs[0])), "Toggle did not select the item.");
        WpfSta.Invoke(() =>
        {
            box.UpdateLayout();
            var container = (TagCheckComboBoxItem)box.ItemContainerGenerator.ContainerFromIndex(0);
            var check = FindChild<CheckBox>(container);
            Assert.IsNotNull(check, "Item row has no CheckBox.");
            Assert.IsTrue(check.IsChecked == true, "Row CheckBox does not follow the selection.");
        });
    }

    [TestMethod]
    public void UiaSelectionItemAddsItem()
    {
        var (box, docs) = CreateBox(5);
        using var host = ThemeHost.Show(box);
        Expand(host, box);

        host.FindInWindow(ItemId(1)).Patterns.SelectionItem.Pattern.AddToSelection();
        host.FindInWindow(ItemId(3)).Patterns.SelectionItem.Pattern.AddToSelection();

        Assert.IsTrue(ThemeHost.WaitFor(() => box.SelectedItems.Count == 2), "AddToSelection did not add both items.");
        WpfSta.Invoke(() => CollectionAssert.AreEquivalent(new[] { docs[1], docs[3] }, box.SelectedItems.Cast<Doc>().ToList()));
    }

    [TestMethod]
    public void ManyItemsStayVirtualized()
    {
        var (box, _) = CreateBox(400);
        using var host = ThemeHost.Show(box);
        Expand(host, box);

        var realized = WpfSta.Invoke(() =>
        {
            box.UpdateLayout();
            return Enumerable.Range(0, box.Items.Count).Count(index => box.ItemContainerGenerator.ContainerFromIndex(index) is not null);
        });

        Assert.IsTrue(realized is > 0 and < 100, "Realized containers: " + realized);
    }

    private static (TagCheckComboBox Box, List<Doc> Docs) CreateBox(int count, double width = 240, int disabled = -1)
    {
        return WpfSta.Invoke(() =>
        {
            var docs = Enumerable.Range(0, count).Select(index => new Doc(index, "Linked model number " + index, index != disabled)).ToList();
            var style = new Style(typeof(TagCheckComboBoxItem), (Style)Resources.Value["TagCheckComboBoxItemBaseStyle"]);
            style.Setters.Add(new Setter(UIElement.IsEnabledProperty, new Binding(nameof(Doc.IsEnabled))));
            style.Setters.Add(new Setter(AutomationProperties.AutomationIdProperty, new Binding(nameof(Doc.Id))));
            style.Setters.Add(new Setter(AutomationProperties.NameProperty, new Binding(nameof(Doc.Label))));
            var box = new TagCheckComboBox
            {
                Width = width,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                DisplayMemberPath = nameof(Doc.Label),
                ItemContainerStyle = style,
                ShowSelectAllButton = true,
                Placeholder = "Documents",
                ItemsSource = docs,
            };
            AutomationProperties.SetAutomationId(box, BoxId);
            return (box, docs);
        });
    }

    private static void SelectAll(TagCheckComboBox box, List<Doc> docs)
    {
        WpfSta.Invoke(() =>
        {
            foreach (var doc in docs)
            {
                box.SelectedItems.Add(doc);
            }

            box.UpdateLayout();
        });
    }

    private static void Expand(ThemeHost host, TagCheckComboBox box)
    {
        host.FindInWindow(BoxId).Patterns.ExpandCollapse.Pattern.Expand();
        Assert.IsTrue(ThemeHost.WaitFor(() => box.IsDropDownOpen && ((Popup)box.Template.FindName("PART_Popup", box)).IsOpen), "Drop-down did not open.");
    }

    private static AutomationElement FindTagRemove(ThemeHost host, string label)
    {
        var box = host.FindInWindow(BoxId);
        var id = BoxId + "." + TagCheckComboBox.TagRemoveIdSuffix;
        var button = box.FindFirstDescendant(cf => cf.ByAutomationId(id).And(cf.ByName("Remove " + label)));
        Assert.IsNotNull(button, "Remove button for " + label + " was not found.");
        return button;
    }

    private static List<string> VisibleLabels(TagCheckComboBox box)
    {
        var labels = new List<string>();
        for (var index = 0; index < box.Items.Count; index++)
        {
            if (box.ItemContainerGenerator.ContainerFromIndex(index) is UIElement { Visibility: Visibility.Visible })
            {
                labels.Add(((Doc)box.Items[index]).Label);
            }
        }

        return labels;
    }

    private static Popup PopupOf(TagCheckComboBox box)
    {
        return (Popup)box.Template.FindName("PART_Popup", box);
    }

    private static T? FindChild<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
            {
                return match;
            }

            if (FindChild<T>(child) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    private static ContentControl Overflow(TagCheckComboBox box)
    {
        return ((TagOverflowPanel)box.Template.FindName("PART_Panel", box)).Overflow!;
    }

    private static string ItemId(int index)
    {
        return "Doc.Item" + index;
    }

    public sealed class Doc(int index, string label, bool isEnabled)
    {
        public string Id { get; } = ItemId(index);

        public string Label { get; } = label;

        public bool IsEnabled { get; } = isEnabled;
    }
}
