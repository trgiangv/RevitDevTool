using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DevTools.Execution.Abstractions;
using Brush = System.Windows.Media.Brush;
using TextElement = System.Windows.Documents.TextElement;
using TextRange = System.Windows.Documents.TextRange;

namespace DevTools.UI.Behaviors;

/// <summary>
/// Attached behavior to highlight matching text in a TextBlock based on a <see cref="TextHighlightRange"/>.
/// </summary>
public static class HighlightTextBlockBehavior
{
    public static readonly DependencyProperty RangeProperty =
        DependencyProperty.RegisterAttached(
            "Range",
            typeof(TextHighlightRange),
            typeof(HighlightTextBlockBehavior),
            new PropertyMetadata(null, OnRangeChanged));

    public static TextHighlightRange? GetRange(DependencyObject obj)
    {
        return (TextHighlightRange?)obj.GetValue(RangeProperty);
    }

    public static void SetRange(DependencyObject obj, TextHighlightRange? value)
    {
        obj.SetValue(RangeProperty, value);
    }

    private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock txtBlock)
            return;

        var range = GetRange(d);
        var normalBackGround = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));

        // Reset highlighting first
        var txtRange = new TextRange(txtBlock.ContentStart, txtBlock.ContentEnd);
        txtRange.ApplyPropertyValue(TextElement.BackgroundProperty, normalBackGround);

        if (range == null || range.Start < 0 || range.End < 0)
            return;

        try
        {
            Brush selectionBackground = new SolidColorBrush(
                range.DarkSkin
                    ? Colors.DarkOrange
                    : Color.FromRgb(0xF5, 0x9E, 0x0B));

            var startPos = txtBlock.ContentStart.GetPositionAtOffset(range.Start + 1);
            var endPos = txtBlock.ContentStart.GetPositionAtOffset(range.End + 1);

            if (startPos == null || endPos == null)
                return;

            var highlightRange = new TextRange(startPos, endPos);
            highlightRange.ApplyPropertyValue(TextElement.BackgroundProperty, selectionBackground);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning($"Error applying text highlight: {ex.Message}");
        }
    }
}
