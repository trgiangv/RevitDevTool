using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DevTools.UI.Behaviors.DragDrop;

public sealed class DragAdorner : Adorner
{
    public const double PreviewOpacity = 0.7;

    private readonly ImageSource _image;
    private readonly Size _size;
    private Point _at;

    internal DragAdorner(UIElement root, FrameworkElement card, Point grab)
        : base(root)
    {
        _image = Snapshot(card);
        _size = new Size(card.ActualWidth, card.ActualHeight);
        Grab = grab;
        _at = card.TranslatePoint(grab, root);
        Card = card;
        IsHitTestVisible = false;
        Opacity = PreviewOpacity;
    }

    public FrameworkElement Card { get; }

    public Point Location => new(_at.X - Grab.X, _at.Y - Grab.Y);

    private Point Grab { get; }

    public void MoveTo(Point pointer)
    {
        if ((pointer - _at).LengthSquared < 0.25)
        {
            return;
        }

        _at = pointer;
        InvalidateVisual();
    }

    internal void FollowCursor()
    {
        if (GetCursorPos(out var point) && PresentationSource.FromVisual(AdornedElement) is not null)
        {
            MoveTo(AdornedElement.PointFromScreen(new Point(point.X, point.Y)));
        }
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        drawingContext.DrawImage(_image, new Rect(Location, _size));
    }

    private static ImageSource Snapshot(FrameworkElement card)
    {
        var dpi = VisualTreeHelper.GetDpi(card);
        var width = Math.Max(1, (int)Math.Ceiling(card.ActualWidth * dpi.DpiScaleX));
        var height = Math.Max(1, (int)Math.Ceiling(card.ActualHeight * dpi.DpiScaleY));
        var bitmap = new RenderTargetBitmap(width, height, dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(new VisualBrush(card), null, new Rect(0, 0, card.ActualWidth, card.ActualHeight));
        }

        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out CursorPoint point);

    [StructLayout(LayoutKind.Sequential)]
    private struct CursorPoint
    {
        public int X;
        public int Y;
    }
}
