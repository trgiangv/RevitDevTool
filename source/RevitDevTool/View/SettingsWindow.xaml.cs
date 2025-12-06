using System.Windows ;
using System.Windows.Controls ;
using System.Windows.Media ;
using RevitDevTool.Extensions ;
using RevitDevTool.Services ;
using RevitDevTool.Theme ;
using Wpf.Ui ;
using Wpf.Ui.Controls ;

namespace RevitDevTool.View ;

public partial class SettingsWindow
{
    public NavigationService NavigationService { get ; private set ; } = null! ;
    public ContentDialogService ContentDialogService { get; private set; } = null!;
    
    public static SettingsWindow? Instance { get; private set; }
    
    public SettingsWindow()
    {
        Instance = this;
        ThemeWatcher.Instance.Watch(this);
        InitializeComponent() ;
        NavigationSetup();
        ContentDialogSetup();
        FixComponentsTheme();
        Closed += OnClosed;
    }

    private static void OnClosed(object? sender, EventArgs e)
    {
        Instance = null;
    }

    private void NavigationSetup()
    {
        var pageProvider = new SimplePageProvider();
        var navService = new NavigationService(pageProvider);
        navService.SetNavigationControl(RootNavigation);
        NavigationService = navService;
    }
    
    private void ContentDialogSetup()
    {
        ContentDialogService = new ContentDialogService();
        ContentDialogService.SetDialogHost(RootContentDialogPresenter);
    }
    
    private void FixComponentsTheme()
    {
        RootNavigation.Loaded += OnNavigationScrollLoaded;
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
        ThemeWatcher.Instance.Watch(scrollViewer);
    }
}