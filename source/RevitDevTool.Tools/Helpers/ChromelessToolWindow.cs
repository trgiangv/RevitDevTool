using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using WpfButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfMenuItem = System.Windows.Controls.MenuItem;
using WpfScrollBar = System.Windows.Controls.Primitives.ScrollBar;
using WpfTextBoxBase = System.Windows.Controls.Primitives.TextBoxBase;

namespace RevitDevTool.Tools.Helpers;

/// <summary>
/// Shared chrome for Acrobat-style tool bars: no title bar, drag on empty area.
/// </summary>
internal static class ChromelessToolWindow
{
    public static void Attach(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        window.WindowStyle = WindowStyle.None;
        window.ResizeMode = ResizeMode.NoResize;
        window.MouseLeftButtonDown += OnDrag;
    }

    private static void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || e.ButtonState != MouseButtonState.Pressed)
            return;
        if (sender is not Window window)
            return;
        if (e.OriginalSource is DependencyObject source && IsInteractive(source))
            return;

        try
        {
            window.DragMove();
        }
        catch
        {
            // DragMove throws if mouse button is already released.
        }
    }

    private static bool IsInteractive(DependencyObject source)
    {
        for (var current = source; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is WpfButtonBase or WpfTextBoxBase or WpfComboBox or WpfMenuItem or WpfScrollBar)
                return true;
        }

        return false;
    }
}
