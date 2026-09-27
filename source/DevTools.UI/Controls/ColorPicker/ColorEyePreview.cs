using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
// ReSharper disable once CheckNamespace
namespace DevTools.UI.Controls;

internal static class ColorEyePreview
{
    public static ToolTip GetPreviewToolTip(ColorEyeDropper target)
    {
        var toolTip = new ToolTip
        {
            PlacementTarget = target,
            Focusable = false,
            Placement = PlacementMode.Relative,
            StaysOpen = true,
            HorizontalOffset = -9999,
            VerticalOffset = -9999,
            IsHitTestVisible = false,
            AllowDrop = false,
            IsOpen = false,
            Visibility = Visibility.Collapsed,
            DataContext = target,
            Content = target.PreviewData
        };

        BindingOperations.SetBinding(
            toolTip,
            ContentControl.ContentTemplateProperty,
            new Binding { Path = new PropertyPath(ColorEyeDropper.PreviewContentTemplateProperty), Source = target });
        return toolTip;
    }

    public static void Show(ToolTip toolTip)
    {
        toolTip.Visibility = Visibility.Visible;
        toolTip.IsOpen = true;
    }

    public static void Hide(ToolTip toolTip)
    {
        toolTip.IsOpen = false;
        toolTip.Visibility = Visibility.Collapsed;
    }

    public static void Move(ToolTip toolTip, Point point, Point offset)
    {
        toolTip.Placement = PlacementMode.Relative;
        toolTip.SetCurrentValue(ToolTip.HorizontalOffsetProperty, point.X + offset.X);
        toolTip.SetCurrentValue(ToolTip.VerticalOffsetProperty, point.Y + offset.Y);
    }
}
