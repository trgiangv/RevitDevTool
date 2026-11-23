using DevTools.ModelsContext.Filters;
using DevTools.UI.Behaviors.DragDrop;
using System.Windows;

namespace DevTools.Presentation.Views.ModelsContext.Filters;

public sealed class FilterDropHandler : IDropTarget
{
    public void DragOver(DropInfo info)
    {
        info.Effects = Target(info) is not null ? DragDropEffects.Move : DragDropEffects.None;
    }

    public void Drop(DropInfo info)
    {
        if (Target(info) is { } target)
        {
            target.Node.MoveTo(target.Group, info.InsertIndex);
        }
    }

    private static (FilterNodeViewModel Node, RuleSetViewModel Group)? Target(DropInfo info)
    {
        return info is { Data: FilterNodeViewModel node, TargetOwner: RuleSetViewModel group } && node.CanDropAt(group, info.InsertIndex)
            ? (node, group)
            : null;
    }
}
