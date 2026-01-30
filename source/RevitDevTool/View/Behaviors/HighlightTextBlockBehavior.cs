using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RevitDevTool.AddinManager.Models;
using Brush = System.Windows.Media.Brush;
using TextElement = System.Windows.Documents.TextElement;
using TextRange = System.Windows.Documents.TextRange;

namespace RevitDevTool.View.Behaviors;

/// <summary>
/// Behavior to highlight matching text in a TextBlock based on search criteria
/// </summary>
public static class HighlightTextBlockBehavior
{
    /// <summary>
    /// Dependency property for the highlight range
    /// </summary>
    public static readonly DependencyProperty RangeProperty =
        DependencyProperty.RegisterAttached(
            "Range",
            typeof(ISelectionRange),
            typeof(HighlightTextBlockBehavior),
            new PropertyMetadata(null, OnRangeChanged));

    public static ISelectionRange? GetRange(DependencyObject obj)
    {
        return (ISelectionRange?)obj.GetValue(RangeProperty);
    }

    public static void SetRange(DependencyObject obj, ISelectionRange? value)
    {
        obj.SetValue(RangeProperty, value);
    }

    private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock txtBlock)
            return;

        var range = GetRange(d);

        // Get transparent background for normal text
        var normalBackGround = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0, 0, 0, 0));
        if (range != null && range.NormalBackground != default)
        {
            normalBackGround = new SolidColorBrush(range.NormalBackground);
        }

        // Reset highlighting first
        var txtRange = new TextRange(txtBlock.ContentStart, txtBlock.ContentEnd);
        txtRange.ApplyPropertyValue(TextElement.BackgroundProperty, normalBackGround);

        if (range == null || range.Start < 0 || range.End < 0)
            return;

        try
        {
            Brush selectionBackground = new SolidColorBrush(range.SelectionBackground);
            
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
