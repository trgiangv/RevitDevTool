using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Parameters;
using DevTools.Presentation.Views.ModelsContext.Filters;
using FlaUI.Core.Definitions;

namespace DevTools.ModelsContext.Views.Tests.Filters;

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

        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Panel.Filter.Categories.Items.All(category => category.IsChecked)),
            "All did not tick every category.");
    }

    [TestMethod]
    public void FilterListsCategories()
    {
        using var ui = PanelUi.Open();
        var load = WpfSta.Invoke(() =>
        {
            ui.Panel.ContextBar.Scope = DevTools.ModelsContext.Scope.ScopeKind.Selection;
            return ui.Panel.RefreshAsync();
        });
        Assert.IsTrue(ThemeHost.WaitFor(() => load.IsCompleted), "The Selection load did not finish.");
        ui.ShowView("ModelContext.View.Filter");
        ui.Host.FindInWindow("Filter.Categories").Patterns.Toggle.Pattern.Toggle();

        Assert.IsTrue(ThemeHost.WaitForUi(() => ui.Host.FindAllInWindow("Filter.Category.Item").Count() > 0),
            "The Categories box lists no categories.");
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
    public void BlankValueShowsSkipped()
    {
        using var ui = OpenFilterWithRule();
        var rule = WpfSta.Invoke(() =>
        {
            var rule = (RuleViewModel)ui.Panel.Filter.Root.Children[0];
            rule.SelectedParameter = rule.Parameters.First(parameter => parameter.DisplayName == "Width");
            return rule;
        });

        Assert.IsTrue(ThemeHost.WaitForUi(() => ui.Host.TryFindInWindow("Filter.Rule.Skipped") is not null),
            "A rule with no value shows no skipped cue.");

        WpfSta.Invoke(() => rule.Value = "300");

        Assert.IsTrue(ThemeHost.WaitForUi(() => ui.Host.TryFindInWindow("Filter.Rule.Skipped") is null),
            "The skipped cue stayed after a value.");
    }

    [TestMethod]
    public void ResultStaysOutsideScroll()
    {
        using var ui = OpenFilter();

        var inside = WpfSta.Invoke(() =>
        {
            var result = FindByAutomationId(ui, "Filter.Result");
            for (System.Windows.DependencyObject? node = result; node is not null; node = System.Windows.Media.VisualTreeHelper.GetParent(node))
            {
                if (node is System.Windows.Controls.ScrollViewer)
                {
                    return true;
                }

                if (node is FilterTabView)
                {
                    return false;
                }
            }

            return false;
        });

        Assert.IsFalse(inside, "The result row scrolls with the rules.");
    }

    [TestMethod]
    public void ReadOnlyHidesAddButtons()
    {
        using var ui = PanelUi.Open();
        ui.ShowView("ModelContext.View.Filter");
        WpfSta.Invoke(() => ui.Panel.Filter.Library.SelectedSource =
            ui.Panel.Filter.Library.Sources.First(source => source.Kind == FilterSourceKind.RuleBased));

        Assert.IsTrue(ThemeHost.WaitFor(() =>
            ui.Panel.Filter.IsReadOnly && ui.Panel.Filter.Library.SelectedSource?.Kind == FilterSourceKind.RuleBased), "The Revit filter did not open read only.");
        Assert.IsTrue(ThemeHost.WaitForUi(() => ui.Host.TryFindInWindow("Filter.Rule.Add") is null),
            "+ Rule shows for a read-only filter.");
    }

    [TestMethod]
    public void ResultShowsMatchCount()
    {
        using var ui = OpenFilter();
        WpfSta.Invoke(() =>
        {
            ui.Panel.Filter.Categories.Items[0].IsChecked = true;
            ui.Clock.Advance(FilterRunner.SelectionSyncDelay);
        });
        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Panel.Filter.Runner.ResultCount > 0), "The filter found nothing.");

        var expected = WpfSta.Invoke(() => ui.Panel.Filter.Runner.ResultCount).ToString(System.Globalization.CultureInfo.InvariantCulture);

        Assert.IsTrue(ThemeHost.WaitForUi(() => Digits(ui.Host.FindInWindow("Filter.ResultCount").Name) == expected),
            "The count shows " + ui.Host.FindInWindow("Filter.ResultCount").Name + ", expected " + expected);
    }

    [TestMethod]
    public void PausedLineShowsOverLimit()
    {
        using var ui = OpenPaused();

        Assert.AreEqual(FilterRunner.SelectionSyncPausedText, ui.Host.FindInWindow("Filter.Paused").Name);
        Assert.AreEqual(ToggleState.On, ui.Host.FindInWindow("Filter.SelectionSync").Patterns.Toggle.Pattern.ToggleState.Value);
    }

    [TestMethod]
    public void PausedLineHidesUnderLimit()
    {
        using var ui = OpenPaused();

        ui.Press("Filter.CategoryNone");
        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Panel.Filter.Categories.Items.All(category => !category.IsChecked)), "None did not untick.");
        WpfSta.Invoke(() => ui.Clock.Advance(FilterRunner.SelectionSyncDelay));

        Assert.IsTrue(ThemeHost.WaitForUi(() => ui.Host.TryFindInWindow("Filter.Paused") is null),
            "The paused line stayed at 0 matches.");
    }

    [TestMethod]
    [DataRow("Filter.Rule.Parameter")]
    [DataRow("Filter.Rule.Operator")]
    public void SmallDropDownItemsAreSmall(string automationId)
    {
        using var ui = OpenFilterWithRule();
        Assert.IsTrue(ThemeHost.WaitForUi(() => ui.Host.TryFindInWindow(automationId) is not null), automationId + " did not show.");
        var box = (System.Windows.Controls.ComboBox)WpfSta.Invoke(() => FindByAutomationId(ui, automationId));
        WpfSta.Invoke(() => box.IsDropDownOpen = true);
        Assert.IsTrue(ThemeHost.WaitForUi(() => WpfSta.Invoke(() => box.ItemContainerGenerator.ContainerFromIndex(0) is not null)),
            automationId + " did not open.");

        var heights = WpfSta.Invoke(() =>
        {
            box.UpdateLayout();
            return Enumerable.Range(0, Math.Min(5, box.Items.Count))
                .Select(index => (System.Windows.FrameworkElement)box.ItemContainerGenerator.ContainerFromIndex(index))
                .Select(item => (item.ActualHeight, Fonts: TextSizes(item)))
                .ToList();
        });

        Assert.IsTrue(heights.All(item => item.ActualHeight <= SmallItemHeight && item.Fonts.All(size => size == SmallItemFontSize)),
            $"{automationId} items: {string.Join("; ", heights.Select(item => $"{item.ActualHeight} px, fonts {string.Join("/", item.Fonts)}"))}.");
    }

    [TestMethod]
    public void ScopeDropDownItemsAreSmall()
    {
        using var ui = OpenFilterWithRule();
        WpfSta.Invoke(() => ui.Panel.Filter.Root.AddGroupCommand.Execute(null));
        Assert.IsTrue(ThemeHost.WaitForUi(() => ui.Host.TryFindInWindow("Filter.Set.Scope.Categories") is not null),
            "The group shows no category box.");
        var box = (DevTools.UI.Controls.TagCheckComboBox)WpfSta.Invoke(() => FindByAutomationId(ui, "Filter.Set.Scope.Categories"));
        WpfSta.Invoke(() => box.IsDropDownOpen = true);
        Assert.IsTrue(ThemeHost.WaitForUi(() => WpfSta.Invoke(() => box.ItemContainerGenerator.ContainerFromIndex(0) is not null)),
            "The category box did not open.");

        var heights = WpfSta.Invoke(() =>
        {
            box.UpdateLayout();
            return Enumerable.Range(0, Math.Min(5, box.Items.Count))
                .Select(index => (System.Windows.FrameworkElement)box.ItemContainerGenerator.ContainerFromIndex(index))
                .Select(item => (item.ActualHeight, Fonts: TextSizes(item)))
                .ToList();
        });

        Assert.IsTrue(heights.All(item => item.ActualHeight <= SmallItemHeight && item.Fonts.All(size => size == SmallItemFontSize)),
            $"Scope items: {string.Join("; ", heights.Select(item => $"{item.ActualHeight} px, fonts {string.Join("/", item.Fonts)}"))}.");
    }

    [TestMethod]
    public void PickerGroupsBySection()
    {
        using var ui = OpenFilterWithRule();
        var box = OpenPicker(ui);

        var groups = WpfSta.Invoke(() => box.Items.Groups!.Cast<System.Windows.Data.CollectionViewGroup>()
            .Select(group => (Name: (ParameterBinding)group.Name, group.ItemCount)).ToList());
        var parameters = WpfSta.Invoke(() => box.Items.Cast<ParameterInfo>().ToList());

        CollectionAssert.AreEqual(new[] { ParameterBinding.Instance, ParameterBinding.Type }, groups.Select(group => group.Name).ToArray());
        Assert.AreEqual(parameters.Count(parameter => parameter.Section == ParameterBinding.Instance), groups[0].ItemCount);
        Assert.AreEqual(parameters.Count(parameter => parameter.IsType), groups[1].ItemCount);
    }

    [TestMethod]
    public void HeadersAreNotListItems()
    {
        using var ui = OpenFilterWithRule();
        var picker = ui.Host.FindInWindow("Filter.Rule.Parameter");
        picker.Patterns.ExpandCollapse.Pattern.Expand();

        Assert.IsTrue(ThemeHost.WaitForUi(() => picker.FindAllDescendants(cf => cf.ByControlType(ControlType.Header)).Length > 0),
            "The picker shows no section header to UI Automation.");
        var header = picker.FindAllDescendants(cf => cf.ByControlType(ControlType.Header))[0];
        StringAssert.StartsWith(header.Name, "Instance", $"Header name '{header.Name}', id '{header.AutomationId}'.");
        Assert.IsFalse(header.Patterns.SelectionItem.IsSupported, "A section header is selectable.");
        Assert.IsFalse(picker.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem))
            .Any(item => item.Name.StartsWith("Instance", StringComparison.Ordinal)), "A section header is a list item.");
    }

    [TestMethod]
    public void HeaderIsNotAnItem()
    {
        using var ui = OpenFilterWithRule();
        var box = OpenPicker(ui);

        var (items, containers) = WpfSta.Invoke(() =>
        {
            box.UpdateLayout();
            var all = box.Items.Cast<object>().ToList();
            var realized = Descendants(box).OfType<System.Windows.Controls.ComboBoxItem>()
                .Concat(Descendants(Popup(box)).OfType<System.Windows.Controls.ComboBoxItem>())
                .Select(item => item.DataContext)
                .ToList();
            return (all, realized);
        });

        Assert.IsTrue(items.All(item => item is ParameterInfo), "The picker items hold something other than parameters.");
        Assert.IsTrue(containers.All(item => item is ParameterInfo), "A selectable row is bound to a section header.");
    }

    [TestMethod]
    public void HeaderStaysOnScroll()
    {
        using var ui = OpenFilterWithRule();
        var box = OpenPicker(ui);

        var (offset, top) = WpfSta.Invoke(() =>
        {
            box.UpdateLayout();
            var popup = Popup(box);
            var scroller = Descendants(popup).OfType<System.Windows.Controls.ScrollViewer>().First();
            scroller.ScrollToVerticalOffset(3);
            scroller.UpdateLayout();
            var header = Descendants(popup).OfType<DevTools.UI.Controls.StickyGroupHeader>().First();
            header.Update();
            return (header.Offset, header.TranslatePoint(new System.Windows.Point(), scroller).Y);
        });

        Assert.IsGreaterThan(0.0, offset, "The Instance header scrolled away with its rows.");
        Assert.AreEqual(0, top, 1, "The Instance header is not at the top of the list.");
    }

    [TestMethod]
    [DataRow(ParameterOrigin.BuiltIn, null)]
    [DataRow(ParameterOrigin.Project, "SuccessBrush")]
    [DataRow(ParameterOrigin.Shared, "PrimaryBrush")]
    [DataRow(ParameterOrigin.Family, "WarningBrush")]
    public void RowIconFollowsSource(ParameterOrigin origin, string? brush)
    {
        using var ui = OpenFilter();

        var (icon, stroke, expected) = WpfSta.Invoke(() =>
        {
            var item = Render(ui, new ParameterInfo(ParameterKey.Named("Width", "spec:length"), "Width", ValueKind.Number,
                Origins: origin));
            var path = Descendants(item).OfType<System.Windows.Shapes.Path>().Single();
            var wanted = brush is null ? null : ui.View.TryFindResource(brush);
            return (path.Visibility, path.Stroke, wanted);
        });

        Assert.AreEqual(brush is null ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible, icon);
        if (brush is not null)
        {
            Assert.AreSame(expected, stroke);
        }
    }

    [TestMethod]
    public void RowShowsNameOnly()
    {
        using var ui = OpenFilter();

        var texts = WpfSta.Invoke(() => Texts(Render(ui, new ParameterInfo(ParameterKey.BuiltIn("Width", "FAMILY_WIDTH_PARAM"),
            "Width", ValueKind.Number, Origins: ParameterOrigin.BuiltIn)
        {
            CategoryNames = ["Doors", "Furniture", "Generic Models", "Windows"]
        })));

        CollectionAssert.AreEqual(new[] { "Width" }, texts, "Categories show in the tooltip, not on the row.");
    }

    [TestMethod]
    public void CollapsedSectionSurvivesReopen()
    {
        using var ui = OpenFilterWithRule();
        var box = OpenPicker(ui);

        var (shownBefore, shownCollapsed, collapsedAfterReopen, state) = WpfSta.Invoke(() =>
        {
            box.UpdateLayout();
            var header = Descendants(Popup(box)).OfType<DevTools.UI.Controls.StickyGroupHeader>().First();
            var group = (System.Windows.Data.CollectionViewGroup)header.Content;
            var before = ShownRows(box, group);
            var peer = (System.Windows.Automation.Provider.IExpandCollapseProvider)
                System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(header);
            peer.Collapse();
            box.UpdateLayout();
            var collapsed = ShownRows(box, group);
            var expandState = peer.ExpandCollapseState;
            box.IsDropDownOpen = false;
            box.IsDropDownOpen = true;
            box.UpdateLayout();
            var again = Descendants(Popup(box)).OfType<DevTools.UI.Controls.StickyGroupHeader>()
                .First(item => Equals(((System.Windows.Data.CollectionViewGroup)item.Content).Name, group.Name));
            return (before, collapsed, again.IsCollapsed, expandState);
        });

        Assert.IsGreaterThan(0, shownBefore, "The first section shows no rows before collapse.");
        Assert.AreEqual(0, shownCollapsed, "A collapsed section still shows rows.");
        Assert.AreEqual(System.Windows.Automation.ExpandCollapseState.Collapsed, state);
        Assert.IsTrue(collapsedAfterReopen, "The section opened again after the picker closed and opened.");
    }

    [TestMethod]
    public void SearchShowsCollapsedSections()
    {
        using var ui = OpenFilterWithRule();
        var box = OpenPicker(ui);

        var (collapsed, searching, cleared) = WpfSta.Invoke(() =>
        {
            box.UpdateLayout();
            var header = Descendants(Popup(box)).OfType<DevTools.UI.Controls.StickyGroupHeader>().First();
            header.SetCollapsed(true);
            var hidden = header.IsExpanded;
            DevTools.UI.Controls.StickyGroupHeader.SetShowAll(box, true);
            var shown = header.IsExpanded;
            DevTools.UI.Controls.StickyGroupHeader.SetShowAll(box, false);
            return (hidden, shown, header.IsExpanded);
        });

        Assert.IsFalse(collapsed);
        Assert.IsTrue(searching, "A search must show every section.");
        Assert.IsFalse(cleared, "The section did not collapse again after the search.");
    }

    private static int ShownRows(System.Windows.Controls.ComboBox box, System.Windows.Data.CollectionViewGroup group)
    {
        return Descendants(Popup(box)).OfType<System.Windows.Controls.ComboBoxItem>()
            .Count(item => item.IsVisible && group.Items.Contains(item.DataContext));
    }

    [TestMethod]
    public void RowHasNoTagsOrWarnings()
    {
        using var ui = OpenFilter();

        var labels = WpfSta.Invoke(() => Descendants(Render(ui, new ParameterInfo(ParameterKey.Named("Width", "spec:length"), "Width",
                ValueKind.Number, SpecLabel: "Length", Origins: ParameterOrigin.Family, Bindings: ParameterBinding.Type)
        {
            Models = ["Architectural"]
        }))
            .OfType<System.Windows.Controls.Label>().Count());

        Assert.AreEqual(0, labels);
    }

    [TestMethod]
    public void TipShowsRowsAndLists()
    {
        using var ui = OpenFilter();
        var info = new ParameterInfo(ParameterKey.BuiltIn("Width", "RBS_CURVE_WIDTH_PARAM"), "Width", ValueKind.Number,
            ParameterStorage.Double, "spec:duct", "Duct Size", ParameterOrigin.BuiltIn)
        {
            Discipline = "HVAC",
            Group = "Dimensions",
            Models = ["Current model"],
            CategoryNames = ["A", "B", "C", "D", "E", "F", "G"]
        };

        var texts = WpfSta.Invoke(() =>
        {
            var filterView = (FilterTabView)ui.View.FindName("FilterView");
            var tip = new System.Windows.Controls.ContentControl
            {
                Content = ParameterTip.Of(info),
                ContentTemplate = (System.Windows.DataTemplate)filterView.Resources["ParameterTipTemplate"]
            };
            Layout(tip);
            return Texts(tip);
        });

        CollectionAssert.IsSubsetOf(new[]
        {
            "Width", "BuiltInParameter", "RBS_CURVE_WIDTH_PARAM", "Discipline", "HVAC", "Type of parameter", "Duct Size",
            "Group", "Dimensions", "Models", "• Current model", "Categories", "• E", "+ 2 more"
        }, texts);
        CollectionAssert.DoesNotContain(texts, "• F");
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
                && box.Items.Count < ui.Panel.Filter.Root.Parameters.Count),
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
    public void ValueBoxShowsExample()
    {
        using var ui = OpenFilterWithRule();
        var rule = (RuleViewModel)ui.Panel.Filter.Root.Children[0];

        WpfSta.Invoke(() => rule.SelectedParameter = rule.Parameters.First(parameter => parameter.DisplayName == "Width"));

        Assert.IsTrue(ThemeHost.WaitForUi(() => WpfSta.Invoke(() =>
                HandyControl.Controls.InfoElement.GetPlaceholder(FindByAutomationId(ui, "Filter.Rule.Value")) == "300 mm")),
            "The value box does not show the example.");
    }

    [TestMethod]
    public void ChosenTypeParameterShowsType()
    {
        using var ui = OpenFilterWithRule();
        var rule = (RuleViewModel)ui.Panel.Filter.Root.Children[0];

        WpfSta.Invoke(() => rule.SelectedParameter = rule.Parameters.First(parameter => parameter.IsType));

        Assert.IsTrue(ThemeHost.WaitForUi(() => ui.Host.TryFindInWindow("Filter.Rule.Parameter.Type") is not null),
            "The chosen type parameter does not show 'Type'.");
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
            filter.Categories.CheckNoneCommand.Execute(null);
            filter.Categories.Items.First(category => category.Name == "Ducts").IsChecked = true;
        });

        Assert.IsTrue(ThemeHost.WaitFor(() => filter.Root.Parameters.All(parameter => parameter.DisplayName != "Swing Angle")),
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
    public void FilterHasNoNotToggle()
    {
        using var ui = OpenFilter();

        Assert.IsNull(ui.Host.TryFindInWindow("Filter.Set.Not"));
    }

    private const double SmallItemHeight = 20;
    private const double SmallItemFontSize = 11;

    private static List<double> TextSizes(System.Windows.DependencyObject root)
    {
        var sizes = new List<double>();
        var queue = new Queue<System.Windows.DependencyObject>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current is System.Windows.Controls.TextBlock { IsVisible: true } text && text.Text.Length > 0)
            {
                sizes.Add(text.FontSize);
            }

            for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(current); index++)
            {
                queue.Enqueue(System.Windows.Media.VisualTreeHelper.GetChild(current, index));
            }
        }

        return sizes;
    }

    private static PanelUi OpenPaused()
    {
        var ui = OpenFilter();
        WpfSta.Invoke(() => ui.Panel.ContextBar.Tick(ui.Model.Data.Documents.Select(document => document.Key)));
        ui.Host.FindInWindow("Filter.Categories").Patterns.Toggle.Pattern.Toggle();
        ui.Press("Filter.CategoryAll");
        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Panel.Filter.Categories.Items.All(category => category.IsChecked)), "All did not tick.");
        WpfSta.Invoke(() => ui.Clock.Advance(FilterRunner.SelectionSyncDelay));
        Assert.IsTrue(ThemeHost.WaitForUi(() => ui.Host.TryFindInWindow("Filter.Paused") is not null),
            "The paused line did not show over the limit.");
        return ui;
    }

    private static PanelUi OpenFilterWithRule()
    {
        var ui = OpenFilter();
        WpfSta.Invoke(() =>
        {
            foreach (var category in ui.Panel.Filter.Categories.Items)
            {
                category.IsChecked = true;
            }

            ui.Panel.Filter.Root.AddConditionCommand.Execute(null);
        });
        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Panel.Filter.Root.Parameters.Any(parameter => parameter.DisplayName == "Width")),
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

    private static System.Windows.Controls.ContentControl Render(PanelUi ui, ParameterInfo info)
    {
        var filterView = (FilterTabView)ui.View.FindName("FilterView");
        var item = new System.Windows.Controls.ContentControl
        {
            Content = info,
            ContentTemplate = (System.Windows.DataTemplate)filterView.Resources["ParameterTemplate"],
            Resources = { MergedDictionaries = { System.Windows.Window.GetWindow(ui.View)!.Resources, filterView.Resources } }
        };
        Layout(item);
        return item;
    }

    private static System.Windows.DependencyObject Popup(System.Windows.Controls.ComboBox box)
    {
        return ((System.Windows.Controls.Primitives.Popup)box.Template.FindName("PART_Popup", box)).Child;
    }

    private static void Layout(System.Windows.FrameworkElement element)
    {
        element.Measure(new System.Windows.Size(300, 1000));
        element.Arrange(new System.Windows.Rect(0, 0, 300, 1000));
        element.UpdateLayout();
    }

    private static List<System.Windows.DependencyObject> Descendants(System.Windows.DependencyObject root)
    {
        var found = new List<System.Windows.DependencyObject>();
        var queue = new Queue<System.Windows.DependencyObject>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            found.Add(current);
            for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(current); index++)
            {
                queue.Enqueue(System.Windows.Media.VisualTreeHelper.GetChild(current, index));
            }
        }

        return found;
    }

    private static List<string> Texts(System.Windows.DependencyObject root)
    {
        return Descendants(root)
            .OfType<System.Windows.Controls.TextBlock>()
            .Where(text => text.Visibility == System.Windows.Visibility.Visible && text.Text.Length > 0)
            .Select(text => text.Text)
            .ToList();
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
        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Panel.Filter.Library.SelectedSource is { IsCustom: true }),
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
            if (current is System.Windows.FrameworkElement { IsVisible: true } element
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
