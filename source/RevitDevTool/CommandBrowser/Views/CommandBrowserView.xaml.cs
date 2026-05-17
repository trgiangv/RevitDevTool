using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using RevitDevTool.CommandBrowser.Models;
using RevitDevTool.CommandBrowser.ViewModels;

namespace RevitDevTool.CommandBrowser.Views;

public partial class CommandBrowserView
{
    private bool _toggleClicked;

    public CommandBrowserView()
    {
        InitializeComponent();
        SearchComboBox.DropDownClosed += OnSearchDropDownClosed;
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SearchComboBox.SelectedItem is not GroupedCommandEntry { Command.IsAvailable: true } entry
            || DataContext is not CommandBrowserViewModel vm)
        {
            SearchComboBox.SelectedIndex = -1;
            return;
        }

        vm.RunCommand.Execute(entry.Command);
        SearchComboBox.SelectedIndex = -1;
    }

    private void HeartButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: GroupedCommandEntry entry }) return;
        if (DataContext is not CommandBrowserViewModel vm) return;

        e.Handled = true;
        vm.ToggleFavoriteCommand.Execute(entry.Command);
    }

    private void FavToggleButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _toggleClicked = SearchComboBox.IsDropDownOpen;
    }

    private void OnSearchDropDownClosed(object? sender, EventArgs e)
    {
        if (!_toggleClicked) return;
        _toggleClicked = false;
        Dispatcher.BeginInvoke(
            new Action(() => SearchComboBox.IsDropDownOpen = true),
            DispatcherPriority.Background);
    }
}
