using System.Windows;

namespace DevTools.UI.Behaviors.DragDrop;

public sealed class DragInfo(object data, UIElement source, Point start)
{
    public object Data { get; } = data;

    public UIElement Source { get; } = source;

    public Point Start { get; } = start;

    internal object? LastItem { get; set; }

    internal InsertPosition? LastPosition { get; set; }

    public bool IsFarEnough(Point current)
    {
        return Math.Abs(current.X - Start.X) >= SystemParameters.MinimumHorizontalDragDistance
               || Math.Abs(current.Y - Start.Y) >= SystemParameters.MinimumVerticalDragDistance;
    }
}
