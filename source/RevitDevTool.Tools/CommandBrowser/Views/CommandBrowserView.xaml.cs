using System.Windows;
using System.Windows.Controls;
using RevitDevTool.Tools.CommandBrowser.Models;
using RevitDevTool.Tools.CommandBrowser.ViewModels;

namespace RevitDevTool.Tools.CommandBrowser.Views;

public partial class CommandBrowserView
{
    public CommandBrowserView()
    {
        InitializeComponent();
        Unloaded += OnUnloaded;
        SearchComboBox.DropDownOpened += OnSearchDropDownOpened;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not CommandBrowserViewModel vm)
            return;

        if (vm.ElementFinder.IsOpen) vm.ElementFinder.IsOpen = false;
    }

    private void OnSearchDropDownOpened(object? sender, EventArgs e)
    {
        if (DataContext is CommandBrowserViewModel vm)
            vm.RefreshAvailability();
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SearchComboBox.SelectedItem is not GroupedCommandEntry { Command.RibbonInfo.IsEnabled: true } entry
            || DataContext is not CommandBrowserViewModel vm)
        {
            SearchComboBox.SelectedIndex = -1;
            return;
        }

        vm.RunCommand.Execute(entry.Command);
        SearchComboBox.SelectedIndex = -1;
    }
}
