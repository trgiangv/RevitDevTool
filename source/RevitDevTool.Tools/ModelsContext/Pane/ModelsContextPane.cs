using DevTools.ModelsContext.Panel;
using RevitDevTool.Core;
using System.Windows;
using System.Windows.Controls;

namespace RevitDevTool.Tools.ModelsContext.Pane;

public sealed class ModelsContextPane(ModelsContextPanel panel)
{
    private static readonly Guid PaneGuid = new("6C1E3B7A-52D4-4F0B-9E57-2E8B1C4D9A31");
    public static readonly DockablePaneId PaneId = new(PaneGuid);

    private readonly ContentControl _host = new();
    private bool _initialized;

    public static bool IsShown => DockablePane.PaneExists(PaneId) && new DockablePane(PaneId).IsShown();

    public void Initialize(UIControlledApplication application)
    {
        if (_initialized)
        {
            return;
        }

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
        panel.Start(application);
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

    public static void Hide()
    {
        if (DockablePane.PaneIsRegistered(PaneId) && IsShown)
        {
            new DockablePane(PaneId).Hide();
        }
    }

    public void Shutdown()
    {
        _host.IsVisibleChanged -= OnVisibleChanged;
        panel.Stop();
        _initialized = false;
    }

    private void OnVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!_initialized)
        {
            return;
        }

        var visible = (bool)e.NewValue;
        panel.IsActive = visible;
        if (visible && !panel.IsBuilt)
        {
            _host.Content = panel.Build();
        }

        if (!visible)
        {
            _ = ModelsContextPanel.DropFiltersAsync(() => IsShown);
        }
    }
}
