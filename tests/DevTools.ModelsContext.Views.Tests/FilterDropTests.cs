using DevTools.ModelsContext.Tests.Shared;
using DevTools.ModelsContext.ViewModels;
using DevTools.Presentation.Views.ModelsContext;
using DevTools.UI.Behaviors.DragDrop;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace DevTools.ModelsContext.Views.Tests;

[TestClass]
[DoNotParallelize]
public sealed class FilterDropTests
{
    private static readonly Lazy<FakeModelData> Data = new(() => FakeModelGenerator.Generate(new FakeModelOptions
    {
        CurrentElements = 500,
        LinkElements = 200,
    }));

    [TestMethod]
    public void DropMovesRuleDown()
    {
        var order = WpfSta.Invoke(() =>
        {
            using var panel = OpenPanel();
            var root = Rules(panel, 3);
            Assert.AreEqual(DragDropEffects.Move, Drop(root.Children[0], root, 3));
            return Names(root);
        });

        CollectionAssert.AreEqual(new[] { "B", "C", "A" }, order);
    }

    [TestMethod]
    public void DropMovesRuleUp()
    {
        var order = WpfSta.Invoke(() =>
        {
            using var panel = OpenPanel();
            var root = Rules(panel, 3);
            Assert.AreEqual(DragDropEffects.Move, Drop(root.Children[2], root, 0));
            return Names(root);
        });

        CollectionAssert.AreEqual(new[] { "C", "A", "B" }, order);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    public void DropOnItselfDoesNothing(int index)
    {
        var (effects, order) = WpfSta.Invoke(() =>
        {
            using var panel = OpenPanel();
            var root = Rules(panel, 3);
            return (Drop(root.Children[0], root, index), Names(root));
        });

        Assert.AreEqual(DragDropEffects.None, effects);
        CollectionAssert.AreEqual(new[] { "A", "B", "C" }, order);
    }

    [TestMethod]
    public void DropMovesIntoNestedGroup()
    {
        WpfSta.Invoke(() =>
        {
            using var panel = OpenPanel();
            var root = Rules(panel, 1);
            var inner = AddGroup(AddGroup(root));
            var rule = root.Children[0];

            Assert.AreEqual(DragDropEffects.Move, Drop(rule, inner, 0));
            Assert.AreSame(inner, rule.Group);
            Assert.HasCount(1, root.Children);
        });
    }

    [TestMethod]
    public void DropMovesOutOfGroup()
    {
        WpfSta.Invoke(() =>
        {
            using var panel = OpenPanel();
            var root = Rules(panel, 1);
            var group = AddGroup(root);
            group.AddConditionCommand.Execute(null);
            var rule = group.Children[0];

            Assert.AreEqual(DragDropEffects.Move, Drop(rule, root, 0));
            Assert.AreSame(root, rule.Group);
            Assert.AreSame(rule, root.Children[0]);
            Assert.IsEmpty(group.Children);
        });
    }

    [TestMethod]
    public void DropMovesAcrossNestedGroups()
    {
        WpfSta.Invoke(() =>
        {
            using var panel = OpenPanel();
            var root = Rules(panel, 0);
            var inner = AddGroup(AddGroup(root));
            inner.AddConditionCommand.Execute(null);
            var other = AddGroup(root);
            var rule = inner.Children[0];

            Assert.AreEqual(DragDropEffects.Move, Drop(rule, other, 0));
            Assert.AreSame(other, rule.Group);
            Assert.IsEmpty(inner.Children);
        });
    }

    [TestMethod]
    public void DropRefusesGroupIntoDescendant()
    {
        var (effects, parent) = WpfSta.Invoke(() =>
        {
            using var panel = OpenPanel();
            var root = Rules(panel, 0);
            var outer = AddGroup(root);
            var inner = AddGroup(AddGroup(outer));
            return (Drop(outer, inner, 0), outer.Group);
        });

        Assert.AreEqual(DragDropEffects.None, effects);
        Assert.IsNotNull(parent);
    }

    [TestMethod]
    [DataRow(20d, InsertPosition.Before)]
    [DataRow(80d, InsertPosition.After)]
    public void ZoneFollowsPointerHalf(double offset, InsertPosition position)
    {
        Assert.AreEqual(position, DropZones.Position(offset, 100, null));
    }

    [TestMethod]
    [DataRow(InsertPosition.After, 40d, InsertPosition.After)]
    [DataRow(InsertPosition.Before, 60d, InsertPosition.Before)]
    [DataRow(InsertPosition.After, 20d, InsertPosition.Before)]
    [DataRow(InsertPosition.Before, 80d, InsertPosition.After)]
    public void SideFlipsOnlyPastBand(InsertPosition previous, double offset, InsertPosition expected)
    {
        Assert.AreEqual(expected, DropZones.Position(offset, 100, previous));
    }

    [TestMethod]
    [DataRow(0.2, 0, InsertPosition.Before)]
    [DataRow(0.8, 1, InsertPosition.After)]
    public void PointerPicksCardSide(double ratio, int index, InsertPosition position)
    {
        using var ui = OpenUi(root => Rules(root, 3));
        var info = WpfSta.Invoke(() =>
        {
            var root = ui.Panel.Filter.Root;
            return At(ui, root.Children[2], root.Children[0], ratio);
        });

        Assert.AreEqual(DragDropEffects.Move, info!.Effects);
        Assert.AreEqual(index, info.InsertIndex);
        Assert.AreEqual(position, info.Position);
    }

    [TestMethod]
    public void SmallMoveBackKeepsLine()
    {
        using var ui = OpenUi(root => Rules(root, 3));
        var (first, second) = WpfSta.Invoke(() =>
        {
            var root = ui.Panel.Filter.Root;
            var drag = DragOf(root.Children[2]);
            var at80 = At(ui, drag, root.Children[0], 0.8)!.Position;
            var at40 = At(ui, drag, root.Children[0], 0.4)!.Position;
            return (at80, at40);
        });

        Assert.AreEqual(InsertPosition.After, first);
        Assert.AreEqual(InsertPosition.After, second);
    }

    [TestMethod]
    [DataRow(0.2)]
    [DataRow(0.8)]
    public void NoLineForNoOpSpot(double ratio)
    {
        using var ui = OpenUi(root => Rules(root, 3));
        var (effects, shown) = WpfSta.Invoke(() =>
        {
            var root = ui.Panel.Filter.Root;
            var info = At(ui, root.Children[1], root.Children[1], ratio)!;
            DragReorder.ShowLine(info);
            var result = (info.Effects, DragReorder.IsLineShown);

            DragReorder.HideLine();
            return result;
        });

        Assert.AreEqual(DragDropEffects.None, effects);
        Assert.IsFalse(shown);
    }

    [TestMethod]
    public void GroupHeaderDropsBeforeGroup()
    {
        using var ui = OpenUi(root =>
        {
            Rules(root, 2);
            AddGroup(root).AddConditionCommand.Execute(null);
        });
        var (owner, index) = WpfSta.Invoke(() =>
        {
            var root = ui.Panel.Filter.Root;
            var info = At(ui, root.Children[0], root.Children[2], 0.1)!;
            return (info.TargetOwner, info.InsertIndex);
        });

        Assert.AreSame(WpfSta.Invoke(() => ui.Panel.Filter.Root), owner);
        Assert.AreEqual(2, index);
    }

    [TestMethod]
    public void RulesAreaDropsIntoGroup()
    {
        using var ui = OpenUi(root =>
        {
            Rules(root, 2);
            AddGroup(root).AddConditionCommand.Execute(null);
        });
        var (owner, group, effects) = WpfSta.Invoke(() =>
        {
            var root = ui.Panel.Filter.Root;
            var group = (RuleSetViewModel)root.Children[2];
            var info = At(ui, root.Children[0], group.Children[0], 0.2)!;
            return (info.TargetOwner, group, info.Effects);
        });

        Assert.AreSame(group, owner);
        Assert.AreEqual(DragDropEffects.Move, effects);
    }

    [TestMethod]
    public void EmptyGroupTakesDrop()
    {
        using var ui = OpenUi(root =>
        {
            Rules(root, 1);
            AddGroup(root);
        });
        var (owner, group, effects) = WpfSta.Invoke(() =>
        {
            var root = ui.Panel.Filter.Root;
            var group = (RuleSetViewModel)root.Children[1];
            var placeholder = Find<StackPanel>(ui.View, panel => panel.Name == "Body" && panel.DataContext == group);
            var host = ListOf(ui, root);
            var point = placeholder.TranslatePoint(new Point(placeholder.ActualWidth / 2, placeholder.ActualHeight / 2), host);
            var info = DragReorder.DropAtPoint(host, DragOf(root.Children[0]), point)!;
            return (info.TargetOwner, group, info.Effects);
        });

        Assert.AreSame(group, owner);
        Assert.AreEqual(DragDropEffects.Move, effects);
    }

    [TestMethod]
    public void PreviewFollowsDragLifetime()
    {
        using var ui = OpenUi(root => Rules(root, 2));
        var (started, dimmed, ended, restored) = WpfSta.Invoke(() =>
        {
            var root = ui.Panel.Filter.Root;
            var card = CardOf(ui, root.Children[0]);
            var preview = DragReorder.StartPreview(card, new Point(4, 4));
            var started = VisualTreeHelper.GetParent(preview!) is AdornerLayer;
            var dimmed = card.Opacity;
            DragReorder.EndPreview();
            var ended = DragReorder.Preview is null && VisualTreeHelper.GetParent(preview) is null;
            return (started, dimmed, ended, card.Opacity);
        });

        Assert.IsTrue(started);
        Assert.AreEqual(DragReorder.DimmedOpacity, dimmed);
        Assert.IsTrue(ended);
        Assert.AreEqual(1d, restored);
    }

    private static PanelUi OpenUi(Action<RuleSetViewModel> build)
    {
        var ui = PanelUi.Open();
        ui.ShowView("ModelContext.View.Filter");
        ui.Press("Filter.Add");
        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Panel.Filter.Library.SelectedSource is { IsCustom: true }), "No custom filter was added.");
        WpfSta.Invoke(() =>
        {
            build(ui.Panel.Filter.Root);
            ui.View.UpdateLayout();
        });
        return ui;
    }

    private static DragInfo DragOf(FilterNodeViewModel node)
    {
        return new DragInfo(node, new Border(), new Point());
    }

    private static DropInfo? At(PanelUi ui, FilterNodeViewModel dragged, FilterNodeViewModel over, double ratio)
    {
        return At(ui, DragOf(dragged), over, ratio);
    }

    private static DropInfo? At(PanelUi ui, DragInfo drag, FilterNodeViewModel over, double ratio)
    {
        var host = ListOf(ui, ui.Panel.Filter.Root);
        var card = CardOf(ui, over);
        var point = card.TranslatePoint(new Point(card.ActualWidth / 2, card.ActualHeight * ratio), host);
        return DragReorder.DropAtPoint(host, drag, point);
    }

    private static FrameworkElement CardOf(PanelUi ui, FilterNodeViewModel node)
    {
        var list = ListOf(ui, node.Group!);
        return (FrameworkElement)list.ItemContainerGenerator.ContainerFromItem(node);
    }

    private static FilterItemsControl ListOf(PanelUi ui, RuleSetViewModel group)
    {
        return Find<FilterItemsControl>(ui.View, list => list.DataContext == group);
    }

    private static T Find<T>(DependencyObject root, Func<T, bool> match)
        where T : DependencyObject
    {
        var queue = new Queue<DependencyObject>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current is T found && match(found))
            {
                return found;
            }

            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(current); index++)
            {
                queue.Enqueue(VisualTreeHelper.GetChild(current, index));
            }
        }

        throw new InvalidOperationException(typeof(T).Name + " was not found.");
    }
    private static DragDropEffects Drop(FilterNodeViewModel node, RuleSetViewModel group, int index)
    {
        var handler = new FilterDropHandler();
        var info = new DropInfo(node, group, group.Children, null, index, InsertPosition.Before);
        handler.DragOver(info);
        if (info.Effects == DragDropEffects.Move)
        {
            handler.Drop(info);
        }

        return info.Effects;
    }

    private static ModelContextViewModel OpenPanel()
    {
        var fake = new FakeHost(new FakeModel(Data.Value));
        var clock = new FakeClock();
        var panel = new ModelContextViewModel(fake.Services(clock, clock));
        panel.Filter.Library.AddCommand.Execute(null);
        return panel;
    }

    private static RuleSetViewModel Rules(ModelContextViewModel panel, int count)
    {
        return Rules(panel.Filter.Root, count);
    }

    private static RuleSetViewModel Rules(RuleSetViewModel root, int count)
    {
        root.Children.Clear();
        for (var index = 0; index < count; index++)
        {
            root.AddConditionCommand.Execute(null);
            ((RuleViewModel)root.Children[^1]).Value = ((char)('A' + index)).ToString();
        }

        return root;
    }

    private static RuleSetViewModel AddGroup(RuleSetViewModel parent)
    {
        parent.AddGroupCommand.Execute(null);
        return (RuleSetViewModel)parent.Children[^1];
    }

    private static string[] Names(RuleSetViewModel group)
    {
        return group.Children.OfType<RuleViewModel>().Select(rule => rule.Value).ToArray();
    }
}
