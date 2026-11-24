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
        var height = MeasureTags(tags, unbounded);
        var visible = FitVisible(tags, availableSize.Width, unbounded, out var width);
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

    private static double MeasureTags(List<UIElement> tags, Size unbounded)
    {
        var height = 0d;
        foreach (var tag in tags)
        {
            tag.Measure(unbounded);
            height = Math.Max(height, tag.DesiredSize.Height);
        }

        return height;
    }

    private int FitVisible(List<UIElement> tags, double available, Size unbounded, out double width)
    {
        width = WidthOf(tags, tags.Count);
        if (width <= available)
        {
            MeasureOverflow(tags, tags.Count, unbounded);
            return tags.Count;
        }

        for (var visible = tags.Count - 1; visible > 0; visible--)
        {
            width = WidthWithOverflow(tags, visible, unbounded);
            if (width <= available)
            {
                return visible;
            }
        }

        if (tags.Count == 0)
        {
            width = WidthWithOverflow(tags, 0, unbounded);
            return 0;
        }

        var overflowWidth = MeasureOverflow(tags, 1, unbounded);
        var rest = tags.Count > 1 ? overflowWidth + Spacing : 0;
        var first = Math.Max(0, available - rest);
        tags[0].Measure(new Size(first, unbounded.Height));
        width = tags[0].DesiredSize.Width + rest;
        return 1;
    }

    private double WidthWithOverflow(List<UIElement> tags, int visible, Size unbounded)
    {
        var overflowWidth = MeasureOverflow(tags, visible, unbounded);
        return WidthOf(tags, visible) + (visible > 0 ? Spacing : 0) + overflowWidth;
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
