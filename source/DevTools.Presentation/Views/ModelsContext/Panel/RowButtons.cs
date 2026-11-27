using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace DevTools.Presentation.Views.ModelsContext.Panel;

public static class RowButtons
{
    public static ButtonBase? ButtonAt(UIElement row, Point point)
    {
        for (var node = VisualTreeHelper.HitTest(row, point)?.VisualHit;
             node is not null && !ReferenceEquals(node, row);
             node = VisualTreeHelper.GetParent(node))
        {
            if (node is ButtonBase button)
            {
                return button;
            }
        }

        return null;
    }

    public static void SwallowDisabledClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is UIElement row && ButtonAt(row, e.GetPosition(row)) is { IsEnabled: false })
        {
            e.Handled = true;
        }
    }
}
