using DevTools.ModelsContext.Diagnostics;
using DevTools.ModelsContext.ViewModels;
using DevTools.Presentation.Views.ModelsContext;
using DevTools.Settings;
using Microsoft.Extensions.Logging;
using RevitDevTool.Core;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace RevitDevTool.Tools.ModelsContext;

public sealed class ModelsContextPane(IFileConfig<PathOptions> fileConfig, ILogger<ModelsContextPane> logger)
{
    private static readonly Guid PaneGuid = new("6C1E3B7A-52D4-4F0B-9E57-2E8B1C4D9A31");
    private static readonly DockablePaneId PaneId = new(PaneGuid);

    private readonly ContentControl _host = new();
    private RevitModelEvents? _events;
    private ModelContextViewModel? _panel;
    private ParameterCache? _parameters;
    private ScopeCache? _scopes;

    public void Initialize(UIControlledApplication application)
    {
        if (_events is not null)
        {
            return;
        }

        RevitProfiler.Use(logger);
        _scopes = new ScopeCache();
        _parameters = new ParameterCache();
        _events = new RevitModelEvents(application, _scopes, _parameters, Dispatcher.CurrentDispatcher);
        _host.IsVisibleChanged += OnVisibleChanged;
        DockablePaneProvider
            .Register(application, PaneGuid, ModelContextViewModel.PanelTitle)
            .SetConfiguration(data =>
            {
                data.FrameworkElement = _host;
                data.VisibleByDefault = false;
                data.InitialState = new DockablePaneState
                {
                    MinimumWidth = 320,
                    DockPosition = DockPosition.Right,
                    TabBehind = DockablePanes.BuiltInDockablePanes.ProjectBrowser
                };
            });
        _events.Subscribe();
    }

    public void ToggleVisibility()
    {
        if (_events is null || !DockablePane.PaneIsRegistered(PaneId) || !DockablePane.PaneExists(PaneId))
        {
            return;
        }

        var pane = new DockablePane(PaneId);
        if (pane.IsShown())
        {
            pane.Hide();
        }
        else
        {
            pane.Show();
        }
    }

    public void Shutdown()
    {
        _host.IsVisibleChanged -= OnVisibleChanged;
        _events?.Unsubscribe();
        _panel?.Dispose();
        _panel = null;
        _events = null;
    }

    private void OnVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_events is null || _scopes is null || _parameters is null)
        {
            return;
        }

        var visible = (bool)e.NewValue;
        _events.IsActive = visible;
        if (visible && _panel is null)
        {
            Build(_events, _scopes, _parameters);
        }
    }

    private void Build(RevitModelEvents events, ScopeCache scopes, ParameterCache parameters)
    {
        using var timer = TraceTimer.Start(logger, "Panel build");
        var services = new ModelContextServices(
            new RevitModelSource(scopes, parameters),
            events,
            new RevitSelectionPort(),
            new RevitViewActions(),
            new RevitFilterEvaluator(parameters),
            new RevitFilterCatalog(),
            new CustomFilterStore(fileConfig),
            new SystemClock(),
            new DispatcherScheduler(_host.Dispatcher),
            logger);
        _panel = new ModelContextViewModel(services) { IsPaneVisible = false };
        var view = new ModelContextView { DataContext = _panel };
        view.SetResourceReference(System.Windows.Controls.Control.BackgroundProperty, "RegionBrush");
        _host.Content = view;
    }
}
