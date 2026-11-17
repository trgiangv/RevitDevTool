using DevTools.ModelsContext.Diagnostics;
using DevTools.ModelsContext.ViewModels;
using DevTools.Presentation.Views.ModelsContext;
using Microsoft.Extensions.Logging;
using RevitDevTool.Core;
using System.Windows;
using System.Windows.Controls;

namespace RevitDevTool.Tools.ModelsContext;

public sealed class ModelsContextPane(ModelContextServices services, IRevitModelEvents events, ILogger<ModelsContextPane> logger)
{
    private static readonly Guid PaneGuid = new("6C1E3B7A-52D4-4F0B-9E57-2E8B1C4D9A31");
    private static readonly DockablePaneId PaneId = new(PaneGuid);

    private readonly ContentControl _host = new();
    private ModelContextViewModel? _panel;
    private bool _initialized;

    public void Initialize(UIControlledApplication application)
    {
        if (_initialized)
        {
            return;
        }

        RevitProfiler.Use(logger);
        _initialized = true;
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
        events.Subscribe(application);
    }
    public void ToggleVisibility()
    {
        if (!_initialized || !DockablePane.PaneIsRegistered(PaneId) || !DockablePane.PaneExists(PaneId))
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
        events.Unsubscribe();
        _panel?.Dispose();
        _panel = null;
        _initialized = false;
    }

    private void OnVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!_initialized)
        {
            return;
        }

        var visible = (bool)e.NewValue;
        events.IsActive = visible;
        if (visible && _panel is null)
        {
            Build();
        }
    }

    private void Build()
    {
        using var timer = TraceTimer.Start(logger, "Panel build");
        _panel = new ModelContextViewModel(services) { IsPaneVisible = false };
        var view = new ModelContextView { DataContext = _panel };
        view.SetResourceReference(System.Windows.Controls.Control.BackgroundProperty, "RegionBrush");
        _host.Content = view;
    }
}
