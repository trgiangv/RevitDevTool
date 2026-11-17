using System.Collections;
using System.Windows;

namespace DevTools.UI.Behaviors.DragDrop;

public enum InsertPosition
{
    Before,
    After
}

public sealed class DropInfo(
    object data,
    object? targetOwner,
    IEnumerable? targetCollection,
    object? targetItem,
    int insertIndex,
    InsertPosition position)
{
    public object Data { get; } = data;

    public object? TargetOwner { get; } = targetOwner;

    public IEnumerable? TargetCollection { get; } = targetCollection;

    public object? TargetItem { get; } = targetItem;

    public int InsertIndex { get; } = insertIndex;

    public InsertPosition Position { get; } = position;

    public DragDropEffects Effects { get; set; } = DragDropEffects.None;

    public UIElement? LineHost { get; internal set; }

    public double LineY { get; internal set; }

    public IDropTarget? Target { get; internal set; }
}
