using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.ViewModels;
using DevTools.Presentation.Views.ModelsContext;
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
        ui.ShowView("ModelContext.View.Filter");
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

    [TestMethod]
    public void PickerListShowsTags()
    {
        using var ui = OpenFilterWithRule();
        var box = OpenPicker(ui);

        var tags = WpfSta.Invoke(() => Tags(box.ItemContainerGenerator.ContainerFromItem(
            box.Items.Cast<ParameterInfo>().First(parameter => parameter.DisplayName == "Mark"))));

        CollectionAssert.IsSubsetOf(new[] { "Built-in", "Instance", "Text" }, tags);
    }

    [TestMethod]
    public void ItemShowsFamilyAndProjectTags()
    {
        using var ui = OpenFilter();

        var tags = RenderItem(ui, new ParameterInfo(ParameterKey.Named("Width", "spec:length"), "Width", ValueKind.Number,
            SpecLabel: "Length", Origins: ParameterOrigin.Family | ParameterOrigin.Project,
            Bindings: ParameterBinding.Instance | ParameterBinding.Type));

        CollectionAssert.IsSubsetOf(new[] { "Project", "Family", "Instance", "Type", "Length" }, tags);
    }

    [TestMethod]
    public void ItemHidesCoverageInAllDocuments()
    {
        using var ui = OpenFilter();
        var info = new ParameterInfo(ParameterKey.Named("Width", "spec:length"), "Width", ValueKind.Number,
            Origins: ParameterOrigin.Project, Coverage: 3, DocumentCount: 3);

        var full = RenderItem(ui, info);
        var partial = RenderItem(ui, info with { Coverage = 2 });

        Assert.IsFalse(full.Any(tag => tag.Contains('/')), "A full-coverage item shows a coverage label.");
        CollectionAssert.Contains(partial, "2/3 docs");
    }

    [TestMethod]
    public void PickerValueSetsParameterName()
    {
        using var ui = OpenFilterWithRule();
        var rule = (RuleViewModel)ui.Panel.Filter.Root.Children[0];
        var picker = ui.Host.FindInWindow("Filter.Rule.Parameter");

        picker.Patterns.Value.Pattern.SetValue("Width");

        Assert.IsTrue(ThemeHost.WaitFor(() => rule.ParameterName == "Width" && rule.Kind == ValueKind.Number),
            "SetValue did not select Width.");
        Assert.AreEqual("Width", picker.Patterns.Value.Pattern.Value.Value);
    }

    [TestMethod]
    public void PickerTypingFiltersIgnoringCase()
    {
        using var ui = OpenFilterWithRule();
        var picker = ui.Host.FindInWindow("Filter.Rule.Parameter");
        var box = (System.Windows.Controls.ComboBox)WpfSta.Invoke(() => FindByAutomationId(ui, "Filter.Rule.Parameter"));

        picker.Patterns.Value.Pattern.SetValue("wID");

        Assert.IsTrue(ThemeHost.WaitFor(() => box.Items.Count > 0
                && box.Items.Cast<ParameterInfo>().All(item => item.DisplayName.Contains("wid", StringComparison.OrdinalIgnoreCase))
                && box.Items.Count < ui.Panel.Filter.Parameters.Count),
            "The picker list was not filtered.");
    }

    [TestMethod]
    public void PickerItemSelectionPicksParameter()
    {
        using var ui = OpenFilterWithRule();
        var rule = (RuleViewModel)ui.Panel.Filter.Root.Children[0];
        var picker = ui.Host.FindInWindow("Filter.Rule.Parameter");

        picker.Patterns.ExpandCollapse.Pattern.Expand();
        var item = picker.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem))
            .First(candidate => candidate.Name.Contains("Mark", StringComparison.Ordinal));
        item.Patterns.SelectionItem.Pattern.Select();

        Assert.IsTrue(ThemeHost.WaitFor(() => rule.SelectedParameter?.DisplayName == "Mark"), "Picking did not set the parameter.");
    }

    [TestMethod]
    public void ReferenceItemShowsElementTag()
    {
        using var ui = OpenFilter();

        var tags = RenderItem(ui, new ParameterInfo(ParameterKey.Named("Level", "storage:id"), "Level", ValueKind.Reference,
            SpecLabel: EnumText.Of(ValueKind.Reference)));

        CollectionAssert.Contains(tags, "Element");
    }

    [TestMethod]
    public void PickerKeepsHiddenParameter()
    {
        using var ui = OpenFilterWithRule();
        var filter = ui.Panel.Filter;
        var rule = (RuleViewModel)filter.Root.Children[0];
        var picker = ui.Host.FindInWindow("Filter.Rule.Parameter");
        picker.Patterns.Value.Pattern.SetValue("Swing Angle");
        Assert.IsTrue(ThemeHost.WaitFor(() => rule.Parameter?.Name == "Swing Angle" && rule.Kind == ValueKind.Number),
            "SetValue did not select Swing Angle.");
        var key = WpfSta.Invoke(() => rule.Parameter);

        WpfSta.Invoke(() =>
        {
            filter.CheckNoneCommand.Execute(null);
            filter.Categories.First(category => category.Name == "Ducts").IsChecked = true;
        });

        Assert.IsTrue(ThemeHost.WaitFor(() => filter.Parameters.All(parameter => parameter.DisplayName != "Swing Angle")),
            "Swing Angle stayed in the list for Ducts.");
        Thread.Sleep(400);
        Assert.AreEqual(key, WpfSta.Invoke(() => rule.Parameter));
        Assert.AreEqual(ValueKind.Number, WpfSta.Invoke(() => rule.Kind));
        Assert.AreEqual("Swing Angle", picker.Patterns.Value.Pattern.Value.Value);
    }

    [TestMethod]
    public void CaseToggleOnlyForText()
    {
        using var ui = OpenFilterWithRule();
        var rule = (RuleViewModel)ui.Panel.Filter.Root.Children[0];

        WpfSta.Invoke(() => rule.SelectedParameter = rule.Parameters.First(parameter => parameter.DisplayName == "Mark"));
        Assert.IsTrue(ThemeHost.WaitForUi(() => ui.Host.TryFindInWindow("Filter.Rule.CaseSensitive") is not null),
            "Aa is missing for a text parameter.");

        WpfSta.Invoke(() => rule.SelectedParameter = rule.Parameters.First(parameter => parameter.DisplayName == "Width"));
        Assert.IsTrue(ThemeHost.WaitForUi(() => ui.Host.TryFindInWindow("Filter.Rule.CaseSensitive") is null),
            "Aa shows for a number parameter.");
    }

    [TestMethod]
    public void YesNoEditorIsNotEditable()
    {
        using var ui = OpenFilterWithRule();
        var rule = (RuleViewModel)ui.Panel.Filter.Root.Children[0];
        Assert.IsTrue(ThemeHost.WaitForUi(() => ui.Host.TryFindInWindow("Filter.Rule.Value")?.Patterns.Value.IsSupported == true),
            "A text rule has no editable value.");

        WpfSta.Invoke(() => rule.SelectedParameter = rule.Parameters.First(parameter => parameter.Kind == ValueKind.YesNo));

        Assert.IsTrue(ThemeHost.WaitForUi(() => ui.Host.TryFindInWindow("Filter.Rule.Value")?.Patterns.Value.IsSupported == false),
            "A yes/no value is editable.");
    }

    [TestMethod]
    public void NotToggleInvertsSet()
    {
        using var ui = OpenFilter();

        ui.Host.FindInWindow("Filter.Set.Not").Patterns.Toggle.Pattern.Toggle();

        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Panel.Filter.Root.IsInverted), "NOT did not invert the set.");
    }

    private static PanelUi OpenFilterWithRule()
    {
        var ui = OpenFilter();
        WpfSta.Invoke(() =>
        {
            foreach (var category in ui.Panel.Filter.Categories)
            {
                category.IsChecked = true;
            }

            ui.Panel.Filter.Root.AddRuleCommand.Execute(null);
        });
        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Panel.Filter.Parameters.Any(parameter => parameter.DisplayName == "Width")),
            "The parameter list has no Width.");
        return ui;
    }

    private static System.Windows.Controls.ComboBox OpenPicker(PanelUi ui)
    {
        Assert.IsTrue(ThemeHost.WaitForUi(() => ui.Host.TryFindInWindow("Filter.Rule.Parameter") is not null),
            "The parameter picker did not show.");
        var box = (System.Windows.Controls.ComboBox)WpfSta.Invoke(() => FindByAutomationId(ui, "Filter.Rule.Parameter"));
        WpfSta.Invoke(() => box.IsDropDownOpen = true);
        Assert.IsTrue(ThemeHost.WaitForUi(() => WpfSta.Invoke(() => box.ItemContainerGenerator.ContainerFromIndex(0) is not null)),
            "The picker list did not open.");
        return box;
    }

    private static List<string> RenderItem(PanelUi ui, ParameterInfo info)
    {
        return WpfSta.Invoke(() =>
        {
            var filterView = (FilterTabView)ui.View.FindName("FilterView");
            var item = new System.Windows.Controls.ContentControl
            {
                Content = info,
                ContentTemplate = (System.Windows.DataTemplate)filterView.Resources["ParameterTemplate"]
            };
            item.Measure(new System.Windows.Size(300, 1000));
            item.Arrange(new System.Windows.Rect(0, 0, 300, 1000));
            item.UpdateLayout();
            return Tags(item);
        });
    }

    private static List<string> Tags(System.Windows.DependencyObject root)
    {
        var tags = new List<string>();
        var queue = new Queue<System.Windows.DependencyObject>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current is System.Windows.Controls.Label { Visibility: System.Windows.Visibility.Visible } label)
            {
                tags.Add(label.Content is System.Windows.Controls.TextBlock block ? block.Text : label.Content?.ToString() ?? string.Empty);
            }

            for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(current); index++)
            {
                queue.Enqueue(System.Windows.Media.VisualTreeHelper.GetChild(current, index));
            }
        }

        return tags;
    }
    private static string Digits(string text)
    {
        return new string(text.Where(char.IsDigit).ToArray());
    }

    private static PanelUi OpenFilter()
    {
        var ui = PanelUi.Open();
        ui.ShowView("ModelContext.View.Filter");
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
