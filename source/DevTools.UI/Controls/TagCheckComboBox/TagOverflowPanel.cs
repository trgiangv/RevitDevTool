using HandyControl.Data;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace DevTools.UI.Controls;

public class TagOverflowPanel : Panel
{
    public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(
        nameof(Spacing),
        typeof(double),
        typeof(TagOverflowPanel),
        new FrameworkPropertyMetadata(ValueBoxes.Double0Box, FrameworkPropertyMetadataOptions.AffectsMeasure));

    private ContentControl? _overflow;

    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    public int VisibleCount { get; private set; }

    public int HiddenCount { get; private set; }

    public ContentControl? Overflow
    {
        get => _overflow;
        set
        {
            if (ReferenceEquals(_overflow, value))
            {
                return;
            }

            if (_overflow is not null)
            {
                Children.Remove(_overflow);
            }

            _overflow = value;
            if (_overflow is not null)
            {
                Children.Add(_overflow);
            }

            InvalidateMeasure();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var tags = Tags();
        var unbounded = new Size(double.PositiveInfinity, availableSize.Height);
        var height = 0d;
        foreach (var tag in tags)
        {
            tag.Measure(unbounded);
            height = Math.Max(height, tag.DesiredSize.Height);
        }

        var visible = tags.Count;
        var width = WidthOf(tags, visible);
        if (width > availableSize.Width)
        {
            visible = tags.Count - 1;
            while (visible >= 0)
            {
                var overflowWidth = MeasureOverflow(tags, visible, unbounded);
                width = WidthOf(tags, visible) + (visible > 0 ? Spacing : 0) + overflowWidth;
                if (visible == 0 || width <= availableSize.Width)
                {
                    break;
                }

                visible--;
            }
        }
        else
        {
            MeasureOverflow(tags, visible, unbounded);
        }

        VisibleCount = visible;
        HiddenCount = tags.Count - visible;
        if (_overflow is not null && HiddenCount > 0)
        {
            height = Math.Max(height, _overflow.DesiredSize.Height);
        }

        return new Size(double.IsInfinity(availableSize.Width) ? width : Math.Min(width, availableSize.Width), height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var tags = Tags();
        var x = 0d;
        for (var index = 0; index < tags.Count; index++)
        {
            var tag = tags[index];
            if (index < VisibleCount)
            {
                tag.Arrange(new Rect(x, Top(tag, finalSize), tag.DesiredSize.Width, tag.DesiredSize.Height));
                x += tag.DesiredSize.Width + Spacing;
            }
            else
            {
                tag.Arrange(new Rect());
            }
        }

        if (_overflow is not null)
        {
            _overflow.Arrange(HiddenCount > 0
                ? new Rect(x, Top(_overflow, finalSize), _overflow.DesiredSize.Width, _overflow.DesiredSize.Height)
                : new Rect());
        }

        return finalSize;
    }

    public bool IsShown(UIElement child)
    {
        return ReferenceEquals(child, _overflow)
            ? HiddenCount > 0
            : Tags().IndexOf(child) is var index && index >= 0 && index < VisibleCount;
    }

    private static double Top(UIElement element, Size finalSize)
    {
        return Math.Max(0, (finalSize.Height - element.DesiredSize.Height) / 2);
    }

    private List<UIElement> Tags()
    {
        var tags = new List<UIElement>(Children.Count);
        foreach (UIElement child in Children)
        {
            if (!ReferenceEquals(child, _overflow))
            {
                tags.Add(child);
            }
        }

        return tags;
    }

    private double WidthOf(List<UIElement> tags, int count)
    {
        var width = 0d;
        for (var index = 0; index < count; index++)
        {
            width += tags[index].DesiredSize.Width;
        }

        return count > 1 ? width + (Spacing * (count - 1)) : width;
    }

    private double MeasureOverflow(List<UIElement> tags, int visible, Size unbounded)
    {
        if (_overflow is null)
        {
            return 0;
        }

        var hidden = tags.Count - visible;
        var text = "+" + hidden;
        if (!Equals(_overflow.Content, text))
        {
            _overflow.Content = text;
            AutomationProperties.SetName(_overflow, text);
        }

        var labels = new List<string>(hidden);
        for (var index = visible; index < tags.Count; index++)
        {
            labels.Add(tags[index] is ContentControl { Content: { } content } ? content.ToString() ?? string.Empty : string.Empty);
        }

        var tip = string.Join(Environment.NewLine, labels);
        if (!Equals(_overflow.ToolTip, tip))
        {
            _overflow.ToolTip = hidden > 0 ? tip : null;
        }

        _overflow.Measure(unbounded);
        return _overflow.DesiredSize.Width;
    }
}
