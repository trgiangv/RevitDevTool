using System.Windows;
using System.Windows.Input;
namespace RevitDevTool.View.Controls;

/// <summary>
/// Event check double click in tree view item
/// </summary>
public class MouseDoubleClick
{
    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.RegisterAttached("Command",
            typeof(ICommand),
            typeof(MouseDoubleClick),
            new UIPropertyMetadata(CommandChanged));

    public static readonly DependencyProperty CommandParameterProperty =
        DependencyProperty.RegisterAttached("CommandParameter",
            typeof(object),
            typeof(MouseDoubleClick),
            new UIPropertyMetadata(null));

    public static void SetCommand(DependencyObject target, ICommand value)
    {
        target.SetValue(CommandProperty, value);
    }

    public static void SetCommandParameter(DependencyObject target, object value)
    {
        target.SetValue(CommandParameterProperty, value);
    }

    public static object GetCommandParameter(DependencyObject target)
    {
        return target.GetValue(CommandParameterProperty);
    }

    private static void CommandChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is not System.Windows.Controls.Control control) return;
        if (e is { NewValue: not null, OldValue: null })
        {
            control.MouseDoubleClick += OnMouseDoubleClick;
        }
        else if (e.NewValue == null && e.OldValue != null)
        {
            control.MouseDoubleClick -= OnMouseDoubleClick;
        }
    }

    private static void OnMouseDoubleClick(object sender, RoutedEventArgs e)
    {
        var control = sender as System.Windows.Controls.Control;
        var command = (ICommand)control!.GetValue(CommandProperty);
        var commandParameter = control.GetValue(CommandParameterProperty);
        command.Execute(commandParameter);
    }
}