using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Tests.Shared;
using DevTools.ModelsContext.ViewModels;
using DevTools.Presentation.Views.ModelsContext;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace DevTools.ModelsContext.Views.Tests;

[TestClass]
[DoNotParallelize]
public sealed class FilterLogicViewTests
{
    [TestMethod]
    public void ConnectorClickSwitchesGroup()
    {
        using var ui = OpenFilter();
        WpfSta.Invoke(() =>
        {
            for (var index = 0; index < 3; index++)
            {
                ui.Panel.Filter.Root.AddConditionCommand.Execute(null);
            }
        });
        Assert.IsTrue(ThemeHost.WaitForUi(() => ui.Host.FindAllInWindow("Filter.Set.Connector").Length == 2),
            "Three conditions do not show two connectors.");

        ui.Host.FindAllInWindow("Filter.Set.Connector")[1].AsButton().Invoke();

        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Panel.Filter.Root.Match == SetMatch.Any), "The group did not switch to OR.");
        Assert.IsTrue(ThemeHost.WaitForUi(() => ui.Host.FindAllInWindow("Filter.Set.Connector").All(button => button.Name == "OR")),
            "Not every connector in the group reads OR.");
    }

    [TestMethod]
    public void ScopeListsParentItems()
    {
        using var ui = OpenFilter();
        WpfSta.Invoke(() =>
        {
            var outer = AddGroup(ui.Panel.Filter.Root);
            outer.SetDocumentScope([FakeModelGenerator.ArchSource]);
            AddGroup(outer);
        });
        Assert.IsTrue(ThemeHost.WaitForUi(() => ui.Host.FindAllInWindow("Filter.Set.Scope.Documents").Length == 2),
            "The two groups do not show two document boxes.");

        var boxes = ui.Host.FindAllInWindow("Filter.Set.Scope.Documents");

        CollectionAssert.AreEqual(new[] { "Arch.rvt" }, ItemNames(boxes[1]));
        Assert.IsGreaterThan(1, ItemNames(boxes[0]).Count);
    }

    [TestMethod]
    [DataRow(304d)]
    [DataRow(600d)]
    public void ScopeTagsStayInsideBox(double width)
    {
        using var ui = OpenScopes(width);

        foreach (var (id, box, tags) in ScopeTags(ui))
        {
            var bounds = box.BoundingRectangle;
            {
                foreach (var tag in tags)
                {
                    var inside = tag.BoundingRectangle;
                    Assert.IsTrue(
                        inside.Left >= bounds.Left && inside.Right <= bounds.Right && inside.Top >= bounds.Top
                        && inside.Bottom <= bounds.Bottom,
                        $"{tag.Name} at {inside} spills out of {id} at {bounds}.");
                }
            }
        }
    }

    [TestMethod]
    [DataRow(304d)]
    [DataRow(600d)]
    public void ScopeTagsHaveEvenInsets(double width)
    {
        using var ui = OpenScopes(width);

        foreach (var (id, box, tags) in ScopeTags(ui))
        {
            var bounds = box.BoundingRectangle;
            var first = tags[0].BoundingRectangle;
            var left = first.Left - bounds.Left;
            var top = first.Top - bounds.Top;
            Assert.IsLessThanOrEqualTo(1, Math.Abs(left - top), $"{id}: left inset {left} but top inset {top}.");
            var gaps = tags.Zip(tags.Skip(1), (a, b) => b.BoundingRectangle.Left - a.BoundingRectangle.Right).ToList();
            Assert.IsTrue(gaps.All(gap => Math.Abs(gap - gaps[0]) <= 1), $"{id}: uneven tag gaps {string.Join(", ", gaps)}.");
        }
    }

    private static PanelUi OpenScopes(double width)
    {
        var ui = OpenFilter(width);
        WpfSta.Invoke(() =>
        {
            var parent = ui.Panel.Filter.Root;
            for (var depth = 0; depth < 3; depth++)
            {
                parent = AddGroup(parent);
                foreach (var option in parent.DocumentOptions.Concat(parent.CategoryOptions).ToList())
                {
                    option.IsChecked = true;
                }
            }
        });
        Assert.IsTrue(ThemeHost.WaitForUi(() => ui.Host.FindAllInWindow("Filter.Set.Scope.Categories").Length == 3),
            "The three groups do not show three category boxes.");
        return ui;
    }

    private static IEnumerable<(string Id, AutomationElement Box, AutomationElement[] Tags)> ScopeTags(PanelUi ui)
    {
        foreach (var id in new[] { "Filter.Set.Scope.Documents", "Filter.Set.Scope.Categories" })
        {
            foreach (var box in ui.Host.FindAllInWindow(id))
            {
                var tags = box.FindAllDescendants(cf => cf.ByAutomationId(id + ".Tag").Or(cf.ByAutomationId(id + ".More")))
                    .OrderBy(tag => tag.BoundingRectangle.Left)
                    .ToArray();
                Assert.IsNotEmpty(tags, id + " shows no tag.");
                yield return (id, box, tags);
            }
        }
    }

    [TestMethod]
    public void OutsideValueShowsWarning()
    {
        using var ui = OpenFilter();
        RuleSetViewModel outer = null!;
        WpfSta.Invoke(() =>
        {
            outer = AddGroup(ui.Panel.Filter.Root);
            AddGroup(outer).SetDocumentScope([FakeModelGenerator.MepSource]);
        });
        Assert.IsTrue(ThemeHost.WaitForUi(() => ui.Host.FindAllInWindow("Filter.Set.Scope.Documents").Length == 2),
            "The two groups do not show two document boxes.");
        Assert.IsNull(ui.Host.TryFindInWindow("Filter.Set.Warning"));

        var arch = ui.Host.FindAllInWindow("Filter.Set.Scope.Documents")[0]
            .FindAllChildren(cf => cf.ByControlType(ControlType.ListItem))
            .First(item => item.Name == "Arch.rvt");
        arch.Patterns.Toggle.Pattern.Toggle();

        Assert.IsTrue(ThemeHost.WaitFor(() => outer.ScopeDocuments.SequenceEqual([FakeModelGenerator.ArchSource])),
            "Ticking Arch.rvt did not narrow the outer group.");
        Assert.IsTrue(ThemeHost.WaitForUi(() => ui.Host.TryFindInWindow("Filter.Set.Warning")?.Name.Contains("MEP-Mech.rvt") == true),
            "The inner group shows no warning for MEP-Mech.rvt.");
    }

    [TestMethod]
    public void AltUpDownReorders()
    {
        using var ui = OpenFilter();
        var root = ui.Panel.Filter.Root;
        WpfSta.Invoke(() =>
        {
            root.AddConditionCommand.Execute(null);
            root.AddGroupCommand.Execute(null);
        });
        var group = WpfSta.Invoke(() => root.Children[1]);
        Assert.IsTrue(ThemeHost.WaitForUi(() => ui.Host.FindAllInWindow("Filter.Item.Grip").Length == 2), "Two grips are not shown.");

        // Logical focus only: a UIA Focus() would bring the test window to the front.
        WpfSta.Invoke(() => FocusManager.SetFocusedElement(ui.Host.Window, Grips(ui).First(grip => grip.IsVisible && grip.DataContext is RuleSetViewModel)));
        Assert.IsTrue(ThemeHost.WaitFor(() => FocusedGrip(ui) is { DataContext: RuleSetViewModel }), "The group grip has no focus.");

        Assert.IsTrue(WpfSta.Invoke(() => Press(ui, Key.Up)), "Alt+Up is not bound on the focused card.");
        Assert.IsTrue(ThemeHost.WaitFor(() => root.Children.IndexOf(group) == 0), "Alt+Up did not move the group up.");

        Assert.IsTrue(WpfSta.Invoke(() => Press(ui, Key.Down)), "Alt+Down is not bound on the focused card.");
        Assert.IsTrue(ThemeHost.WaitFor(() => root.Children.IndexOf(group) == 1), "Alt+Down did not move the group down.");
    }

    [TestMethod]
    public void CollapseHidesChildren()
    {
        using var ui = OpenFilter();
        RuleSetViewModel group = null!;
        WpfSta.Invoke(() =>
        {
            group = AddGroup(ui.Panel.Filter.Root);
            group.AddConditionCommand.Execute(null);
        });
        Assert.IsTrue(ThemeHost.WaitForUi(() => ui.Host.FindAllInWindow("Filter.Rule.Parameter").Length == 1),
            "The group condition is not shown.");

        ui.Host.FindInWindow("Filter.Set.Collapse").Patterns.Toggle.Pattern.Toggle();

        Assert.IsTrue(ThemeHost.WaitFor(() => !group.IsExpanded), "The toggle did not collapse the group.");
        Assert.IsTrue(ThemeHost.WaitForUi(() => ui.Host.FindAllInWindow("Filter.Rule.Parameter").Length == 0),
            "A collapsed group still shows its condition.");
    }

    [TestMethod]
    public void AddButtonsReadConditionGroup()
    {
        using var ui = OpenFilter();

        Assert.AreEqual("Condition", ui.Host.FindInWindow("Filter.Rule.Add").Name);
        Assert.AreEqual("Group", ui.Host.FindInWindow("Filter.Set.Add").Name);
    }

    private static List<string> ItemNames(AutomationElement box)
    {
        return box.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem)).Select(item => item.Name).ToList();
    }

    private static List<System.Windows.Controls.Primitives.Thumb> Grips(PanelUi ui)
    {
        var grips = new List<System.Windows.Controls.Primitives.Thumb>();
        var queue = new Queue<DependencyObject>([ui.Host.Window]);
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            if (node is System.Windows.Controls.Primitives.Thumb thumb
                && System.Windows.Automation.AutomationProperties.GetAutomationId(thumb) == "Filter.Item.Grip")
            {
                grips.Add(thumb);
            }

            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(node); index++)
            {
                queue.Enqueue(VisualTreeHelper.GetChild(node, index));
            }
        }

        return grips;
    }

    private static System.Windows.Controls.Primitives.Thumb? FocusedGrip(PanelUi ui)
    {
        return FocusManager.GetFocusedElement(ui.Host.Window) as System.Windows.Controls.Primitives.Thumb
               ?? Keyboard.FocusedElement as System.Windows.Controls.Primitives.Thumb;
    }

    private static bool Press(PanelUi ui, Key key)
    {
        for (DependencyObject? node = FocusedGrip(ui); node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is not UIElement element)
            {
                continue;
            }

            var binding = element.InputBindings.OfType<KeyBinding>()
                .FirstOrDefault(item => item.Gesture is KeyGesture { Modifiers: ModifierKeys.Alt } gesture && gesture.Key == key);
            if (binding?.Command is { } command && command.CanExecute(binding.CommandParameter))
            {
                command.Execute(binding.CommandParameter);
                return true;
            }
        }

        return false;
    }

    private static RuleSetViewModel AddGroup(RuleSetViewModel parent)
    {
        parent.AddGroupCommand.Execute(null);
        return (RuleSetViewModel)parent.Children[^1];
    }

    private static PanelUi OpenFilter(double width = 320)
    {
        var ui = PanelUi.Open(width);
        WpfSta.Run(async () =>
        {
            ui.Panel.ContextBar.Tick(ui.Model.Data.Documents.Select(document => document.Key));
            await ui.Panel.RefreshAsync();
        });
        ui.ShowView("ModelContext.View.Filter");
        ui.Press("Filter.Add");
        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Panel.Filter.Library.SelectedSource is { IsCustom: true }),
            "No custom filter was added.");
        return ui;
    }
}
