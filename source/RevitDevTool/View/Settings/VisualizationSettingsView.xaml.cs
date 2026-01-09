using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using RevitDevTool.Extensions;
using RevitDevTool.Services;
using RevitDevTool.Theme;
using RevitDevTool.View.Settings.Visualization;
using Wpf.Ui;
using Wpf.Ui.Controls;
namespace RevitDevTool.View.Settings;

public partial class VisualizationSettingsView
{
    private NavigationService? _navigationService;
    private bool _isInitialized;

    public VisualizationSettingsView()
    {
        Host.GetService<IThemeWatcherService>().Watch(this);
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        RootNavigation.Loaded += OnNavigationScrollLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var navigationServiceFactory = Host.GetService<INavigationServiceFactory>();
        _navigationService = navigationServiceFactory.Create(RootNavigation);
        RootNavigation.ApplyTemplate();
        if (_isInitialized) return;
        _isInitialized = true;
        Dispatcher.BeginInvoke(
            () =>
            {
                if (!IsLoaded) return;
                _navigationService?.Navigate(typeof(BoundingBoxVisualizationSettingsView));
            },
            DispatcherPriority.Loaded
        );
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = false;
        _navigationService = null;
    }

    private void OnNavigationScrollLoaded(object sender, RoutedEventArgs args)
    {
        var contentPresenter = RootNavigation.FindVisualChild<NavigationViewContentPresenter>();
        if (contentPresenter == null) return;
        contentPresenter.LoadCompleted += ContentPresenterOnContentRendered;
    }
    
    private static void ContentPresenterOnContentRendered(object? sender, EventArgs e)
    {
        var contentPresenter = (NavigationViewContentPresenter) sender!;
        if (!contentPresenter.IsDynamicScrollViewerEnabled) return;

        if (VisualTreeHelper.GetChildrenCount(contentPresenter) == 0)
        {
            contentPresenter.ApplyTemplate();
        }

        var scrollViewer = (ScrollViewer) VisualTreeHelper.GetChild(contentPresenter, 0);
        UpdateScrollViewerResources(scrollViewer);
    }
    
    private static void UpdateScrollViewerResources(ScrollViewer scrollViewer)
    {
        if (UiApplication.Current.Resources.MergedDictionaries.Count < 2)
            return;

        var merged = scrollViewer.Resources.MergedDictionaries;
        var globalTheme = UiApplication.Current.Resources.MergedDictionaries[0];
        var globalControls = UiApplication.Current.Resources.MergedDictionaries[1];

        // Avoid duplicates
        if (merged.Contains(globalTheme) && merged.Contains(globalControls))
            return;

        while (merged.Contains(globalTheme)) merged.Remove(globalTheme);
        while (merged.Contains(globalControls)) merged.Remove(globalControls);

        merged.Insert(0, globalTheme);
        merged.Insert(1, globalControls);
    }
}


