using System.Diagnostics.CodeAnalysis;
using Autodesk.Revit.DB.Events;
using DevTools.Logging.Listeners;
using DevTools.Utilities;
using RevitDevTool.Commands;
using RevitDevTool.Core;
using RevitDevTool.Utils;
using DevTools.Presentation.ViewModels;
using RevitDevTool.View;

namespace RevitDevTool.Controllers;

public sealed class PanelController(LogViewModel logViewModel)
{
    private static readonly Guid PaneGuid = new("43AE2B41-0BE6-425A-B27A-724B2CE17351");
    private static readonly DockablePaneId PaneId = new(PaneGuid);

    private enum DisplayMode
    {
        Uninitialized,
        Docked,
        Floating,
        Hidden,
        Inactive
    }

    private DisplayMode _displayMode = DisplayMode.Uninitialized;
    private bool _paneRegistered;
    private UIControlledApplication? _application;
    private MainWindow? _floatingWindow;

    public static bool HasUiDocument => RevitContext.UiApplication.HasActiveUiDocument();

    public void Initialize(UIControlledApplication application)
    {
        _application = application;
        logViewModel.Subscribe();

        DockablePaneProvider
            .Register(application, PaneGuid, DevToolsCommand.CommandName)
            .SetConfiguration(data =>
            {
                data.FrameworkElement = Host.GetService<MainPage>();
                data.InitialState = new DockablePaneState
                {
                    MinimumWidth = 550,
                    MinimumHeight = 600,
                    DockPosition = DockPosition.Right,
                    TabBehind = DockablePanes.BuiltInDockablePanes.PropertiesPalette
                };
            });

        _paneRegistered = true;

        application.ControlledApplication.DocumentOpened += OnDocumentOpened;
        application.ControlledApplication.DocumentClosed += OnDocumentClosed;
        NotifyListener.TraceReceived += OnTraceReceived;

        _displayMode = HasUiDocument ? DisplayMode.Docked : DisplayMode.Inactive;
    }

    public void Shutdown()
    {
        NotifyListener.TraceReceived -= OnTraceReceived;

        if (_application is not null)
        {
            _application.ControlledApplication.DocumentOpened -= OnDocumentOpened;
            _application.ControlledApplication.DocumentClosed -= OnDocumentClosed;
            _application = null;
        }

        CloseFloatingWindow();
        logViewModel.Dispose();
    }

    public void TogglePaneVisibility()
    {
        if (!TryGetDockablePane(out var pane))
            return;

        if (pane.IsShown())
        {
            pane.Hide();
            _displayMode = DisplayMode.Hidden;
        }
        else
        {
            logViewModel.Subscribe();
            pane.Show();
            _displayMode = DisplayMode.Docked;
        }
    }

    public void ToggleFloatingWindow()
    {
        if (_floatingWindow != null)
        {
            CloseFloatingWindow();
            _displayMode = DisplayMode.Inactive;
        }
        else
        {
            logViewModel.Subscribe();
            ShowFloatingWindow();
            _displayMode = DisplayMode.Floating;
        }
    }

    private void OnTraceReceived()
    {
        if (!logViewModel.IsStarted) return;
        if (_displayMode == DisplayMode.Hidden) return;

        if (HasUiDocument)
        {
            CloseFloatingWindow();

            if (!TryGetDockablePane(out var pane)) return;
            if (!pane.IsShown()) pane.Show();

            _displayMode = DisplayMode.Docked;
            return;
        }

        if (_floatingWindow != null) return;
        if (_displayMode == DisplayMode.Docked) return;

        logViewModel.Subscribe();
        ShowFloatingWindow();
        _displayMode = DisplayMode.Floating;
    }

    private void OnDocumentOpened(object? sender, DocumentOpenedEventArgs args)
    {
        if (!HasUiDocument) return;

        CloseFloatingWindow();

        if (!TryGetDockablePane(out var pane))
        {
            _displayMode = DisplayMode.Docked;
            return;
        }

        if (_displayMode == DisplayMode.Hidden)
        {
            pane.Hide();
        }
        else
        {
            _displayMode = DisplayMode.Docked;
            if (!pane.IsShown()) pane.Show();
        }
    }

    private void OnDocumentClosed(object? sender, DocumentClosedEventArgs args)
    {
        if (HasUiDocument) return;
        _displayMode = DisplayMode.Inactive;
    }

    private void ShowFloatingWindow()
    {
        if (_floatingWindow != null) return;

        DispatcherHelper.RunOnMainThread(() =>
        {
            if (_floatingWindow != null) return;
            _floatingWindow = Host.GetService<MainWindow>();
            _floatingWindow.Closed += OnFloatingWindowClosed;
            _floatingWindow.SetHostAppOwner();
            _floatingWindow.Show();
        });
    }

    private void CloseFloatingWindow()
    {
        if (_floatingWindow is null) return;

        DispatcherHelper.RunOnMainThread(() =>
        {
            if (_floatingWindow is null) return;
            _floatingWindow.Closed -= OnFloatingWindowClosed;
            _floatingWindow.Close();
            _floatingWindow = null;
        });
    }

    private void OnFloatingWindowClosed(object? sender, EventArgs e)
    {
        if (_floatingWindow == null) return;
        _floatingWindow.Closed -= OnFloatingWindowClosed;
        _floatingWindow = null;
        _displayMode = DisplayMode.Inactive;
    }

    private bool TryGetDockablePane([NotNullWhen(true)] out DockablePane? pane)
    {
        pane = null;
        if (!_paneRegistered) return false;

        try
        {
            pane = RevitContext.UiApplication.GetDockablePane(PaneId);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
