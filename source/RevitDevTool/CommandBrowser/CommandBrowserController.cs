using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using Autodesk.Revit.DB.Events;
using Autodesk.Windows;
using DevTools.Utilities;
using RevitDevTool.CommandBrowser.Services;
using RevitDevTool.CommandBrowser.ViewModels;
using RevitDevTool.CommandBrowser.Views;
using Grid = System.Windows.Controls.Grid;

namespace RevitDevTool.CommandBrowser;

/// <summary>
/// Manages the Command Browser bar by injecting a compact search control
/// into the top of Revit's document area (BuiltIn mode).
/// Uses visual tree walking to find AvalonDock's document pane grid.
/// Re-injects automatically when documents are opened/closed (the visual tree rebuilds).
/// </summary>
public sealed class CommandBrowserController(
    RibbonSnoopService snoopService,
    CommandBrowserCache cache,
    CommandBrowserViewModel viewModel)
{
    private const string ControlName = "DevToolsCommandBrowser";
    private bool _initialized;
    private bool _loaded;
    private bool _desiredVisible;
    private UIControlledApplication? _application;

    public void Initialize(UIControlledApplication application)
    {
        if (_initialized) return;

        _application = application;
        _desiredVisible = cache.IsBarVisible;
        _initialized = true;

        application.ControlledApplication.DocumentOpened += OnDocumentOpened;

        if (_desiredVisible)
            ScheduleRetry();
    }

    public void Shutdown()
    {
        cache.IsBarVisible = _desiredVisible;
        cache.Save();

        if (_application is not null)
        {
            _application.ControlledApplication.DocumentOpened -= OnDocumentOpened;
        }

        Remove();
        snoopService.Dispose();
        viewModel.Dispose();
        _initialized = false;
    }

    /// <summary>
    /// Toggles the command browser bar visibility in the document area.
    /// </summary>
    public void ToggleVisibility()
    {
        if (!_initialized) return;

        if (_desiredVisible || IsAdded())
        {
            _desiredVisible = false;
            Hide();
        }
        else
        {
            _desiredVisible = true;
            Show();
        }

        cache.IsBarVisible = _desiredVisible;
        cache.Save();
    }

    private void EnsureLoaded()
    {
        if (_loaded) return;

        snoopService.SnoopAll();
        snoopService.StartTracking();
        cache.Load(snoopService.AllCommands);
        viewModel.InitializeView();
        _loaded = true;
    }

    private void Show()
    {
        HostUiHelper.RunOnMainThread(() =>
        {
            EnsureLoaded();
            if (TryAdd())
                return;

            ScheduleRetry();
        });
    }

    private static void Hide()
    {
        HostUiHelper.RunOnMainThread(Remove);
    }

    /// <summary>
    /// Injects the command browser bar at the top of the document pane.
    /// Creates a fresh view instance each time (WPF requires a clean logical parent).
    /// </summary>
    private bool TryAdd()
    {
        if (IsAdded()) return true;

        var grid = FindDocumentGrid();
        if (grid is null) return false;

        // Only inject when the grid is in its pristine 2-row state
        if (grid.RowDefinitions.Count != 2) return false;

        var background = grid.Children.OfType<Border>().FirstOrDefault(b => string.IsNullOrEmpty(b.Name));
        var contentPanel = grid.Children.OfType<Border>().FirstOrDefault(b => b.Name == "ContentPanel");
        var tabStrip = grid.Children.OfType<Grid>().FirstOrDefault();
        if (background is null || contentPanel is null || tabStrip is null)
            return false;

        var control = new CommandBrowserView
        {
            Name = ControlName,
            DataContext = viewModel
        };

        // Insert a new Auto-height row between the tab strip (row 0) and content (row 1)
        grid.RowDefinitions.Insert(1, new RowDefinition { Height = GridLength.Auto });

        // Background border spans all 3 rows
        Grid.SetRowSpan(background, 3);

        // Content panel moves to row 2
        Grid.SetRow(contentPanel, 2);

        // Tab strip stays at row 0
        Grid.SetRow(tabStrip, 0);

        // Add our control at row 1
        grid.Children.Add(control);
        Grid.SetRow(control, 1);
        return true;
    }

    /// <summary>
    /// Removes the command browser bar from the document pane, restoring the original 2-row grid.
    /// first remove children, then fix row definitions and layout.
    /// </summary>
    private static void Remove()
    {
        var grid = FindDocumentGrid();
        if (grid is null) return;

        // Remove extra row if we added one (grid should have > 2 rows)
        if (grid.RowDefinitions.Count > 2)
        {
            // Our Auto row could be at index 1 (top) or 2 (bottom)
            var removeIndex = grid.RowDefinitions[2].Height == GridLength.Auto ? 2 : 1;
            grid.RowDefinitions.RemoveAt(removeIndex);
        }

        // Restore background border span to 2
        Grid.SetRowSpan(
            grid.Children.OfType<Border>().First(b => string.IsNullOrEmpty(b.Name)),
            2);

        // Content panel back to row 1
        Grid.SetRow(
            grid.Children.OfType<Border>().First(b => b.Name == "ContentPanel"),
            1);

        // Remove our injected controls
        var toRemove = grid.Children.OfType<FrameworkElement>()
            .Where(e => e.Name == ControlName)
            .ToList();
        foreach (var element in toRemove)
            grid.Children.Remove(element);
    }

    private static bool IsAdded()
    {
        var grid = FindDocumentGrid();
        if (grid is null) return false;

        foreach (var child in grid.Children)
        {
            if (child is FrameworkElement { Name: ControlName })
                return true;
        }

        return false;
    }

    private void OnDocumentOpened(object? sender, DocumentOpenedEventArgs e)
    {
        if (!_initialized || !_desiredVisible) return;
        ScheduleRetry();
    }

    private void ScheduleRetry()
    {
        if (!_desiredVisible) return;

        HostUiHelper.HostDispatcher?.BeginInvoke((Action)(() =>
        {
            if (!_initialized || !_desiredVisible) return;
            EnsureLoaded();
            TryAdd();
        }), DispatcherPriority.ApplicationIdle);
    }

    /// <summary>
    /// Navigates Revit's internal visual tree to find the document pane grid.
    /// Path: MainWindow -> LayoutDocumentPaneGroupControl -> LayoutDocumentPaneControl -> Grid
    /// </summary>
    private static Grid? FindDocumentGrid()
    {
        try
        {
            var hwndSource = HwndSource.FromHwnd(ComponentManager.ApplicationWindow);
            if (hwndSource?.RootVisual is not { } root) return null;

            var docGroupControl = FindChildByTypeName(root, "LayoutDocumentPaneGroupControl");
            if (docGroupControl is null) return null;

            var docPaneControl = FindChildByTypeName(docGroupControl, "LayoutDocumentPaneControl");
            return docPaneControl is null ? null : FindVisualChildren<Grid>(docPaneControl).FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static DependencyObject? FindChildByTypeName(DependencyObject parent, string typeName)
    {
        var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child.GetType().Name == typeName)
                return child;

            var result = FindChildByTypeName(child, typeName);
            if (result is not null)
                return result;
        }

        return null;
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
                yield return match;

            foreach (var grandChild in FindVisualChildren<T>(child))
                yield return grandChild;
        }
    }
}
