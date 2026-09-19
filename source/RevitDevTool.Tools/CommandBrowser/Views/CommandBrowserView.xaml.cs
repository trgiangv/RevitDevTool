using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using RevitDevTool.Tools.CommandBrowser.Models;
using RevitDevTool.Tools.CommandBrowser.ViewModels;
using RevitDevTool.Tools.ElementFinder;

namespace RevitDevTool.Tools.CommandBrowser.Views;

public partial class CommandBrowserView
{
    private bool _toggleClicked;
    private ElementFinderViewModel? _elementFinder;

    public CommandBrowserView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SearchComboBox.DropDownOpened += OnSearchDropDownOpened;
        SearchComboBox.DropDownClosed += OnSearchDropDownClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not CommandBrowserViewModel vm)
            return;

        _elementFinder = vm.ElementFinder;
        _elementFinder.PropertyChanged += OnElementFinderPropertyChanged;
        SyncToggles();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_elementFinder is not null)
            _elementFinder.PropertyChanged -= OnElementFinderPropertyChanged;
    }

    private void OnElementFinderPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ElementFinderViewModel.IsOpen))
            ElementFinderToggle.IsChecked = _elementFinder?.IsOpen == true;
    }

    private void SyncToggles()
    {
        ElementFinderToggle.IsChecked = _elementFinder?.IsOpen == true;
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

    private void OnElementFinderToggle(object sender, RoutedEventArgs e)
    {
        _elementFinder?.Toggle();
        if (sender is System.Windows.Controls.Primitives.ToggleButton toggle)
            toggle.IsChecked = _elementFinder?.IsOpen == true;
    }
}
