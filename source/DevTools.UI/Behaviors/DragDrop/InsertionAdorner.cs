using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace DevTools.UI.Behaviors.DragDrop;

internal sealed class InsertionAdorner : Adorner
{
    private readonly Pen _pen;
    private double _y;

    public InsertionAdorner(UIElement adornedElement, Brush brush)
        : base(adornedElement)
    {
        _pen = new Pen(brush, 2);
        IsHitTestVisible = false;
    }

    public double Y
    {
        get => _y;
        set
        {
            if (Math.Abs(_y - value) > 0.1)
            {
                _y = value;
                InvalidateVisual();
            }
        }
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var width = AdornedElement.RenderSize.Width;
        var y = Math.Max(1, Math.Min(_y, AdornedElement.RenderSize.Height - 1));
        drawingContext.DrawLine(_pen, new Point(0, y), new Point(width, y));
    }
}
