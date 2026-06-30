using System.Diagnostics.CodeAnalysis;
using System.Windows.Controls;
using Autodesk.Revit.DB.Events;
using DevTools.Logging.Listeners;
using DevTools.Utilities;
using DevTools.Presentation.ViewModels;
using Microsoft.Extensions.Logging;
using RevitDevTool.Commands;
using RevitDevTool.Core;
using RevitDevTool.Utils;
using RevitDevTool.View;
using ZLogger;

namespace RevitDevTool.Controllers;

public sealed class PanelController(LogViewModel logViewModel, ILogger<PanelController> logger)
{
    private static readonly Guid PaneGuid = new("43AE2B41-0BE6-425A-B27A-724B2CE17351");
    private static readonly DockablePaneId PaneId = new(PaneGuid);

    private readonly ContentControl _paneProxy = new();

    private bool _paneRegistered;
    private bool _userHidePane;
    private UIControlledApplication? _application;
    private MainWindow? _floatingWindow;
    private MainPage? _mainPage;

    public static bool HasUiDocument => RevitContext.UiApplication.HasActiveUiDocument();

    public void Initialize(UIControlledApplication application)
    {
        if (_paneRegistered) return;

        _application = application;
        _mainPage = Host.GetService<MainPage>();
        _paneProxy.Content = _mainPage;

        DockablePaneProvider
            .Register(application, PaneGuid, DevToolsCommand.CommandName)
            .SetConfiguration(data =>
            {
                data.FrameworkElement = _paneProxy;
                data.InitialState = new DockablePaneState
                {
                    MinimumWidth = 550,
                    MinimumHeight = 600,
                    DockPosition = DockPosition.Right,
                    TabBehind = DockablePanes.BuiltInDockablePanes.PropertiesPalette
                };
            });

        _paneRegistered = true;

        NotifyListener.TraceReceived += OnTraceReceived;
        logViewModel.Subscribe();

        application.ControlledApplication.DocumentOpened += OnDocumentOpened;
    }

    public void Shutdown()
    {
        NotifyListener.TraceReceived -= OnTraceReceived;

        if (_application is not null)
        {
            _application.ControlledApplication.DocumentOpened -= OnDocumentOpened;
            _application = null;
        }

        CloseFloatingWindow();
        logViewModel.Dispose();
    }

    public void TogglePaneVisibility()
    {
        if (!TryGetDockablePane(out var pane)) return;

        if (pane.IsShown())
        {
            pane.Hide();
            _userHidePane = true;
        }
        else
        {
            pane.Show();
            _userHidePane = false;
        }
    }

    public void ToggleFloatingWindow()
    {
        HostUiHelper.RunOnMainThread(() =>
        {
            if (_floatingWindow != null)
            {
                CloseFloatingWindow();
            }
            else
            {
                ShowFloatingWindow();
            }
        });
    }

    private void OnTraceReceived()
    {
        if (!logViewModel.IsStarted) return;
        if (_userHidePane) return;

        if (HasUiDocument)
        {
            if (!TryGetDockablePane(out var pane)) return;
            if (!pane.IsShown()) pane.Show();
            return;
        }

        if (_floatingWindow != null || _mainPage is null) return;

        HostUiHelper.RunOnMainThread(ShowFloatingWindow);
    }

    private void OnDocumentOpened(object? sender, DocumentOpenedEventArgs args)
    {
        if (!HasUiDocument) return;

        HostUiHelper.RunOnMainThread(() =>
        {
            CloseFloatingWindow();

            if (!TryGetDockablePane(out var pane)) return;

            if (_userHidePane)
            {
                pane.Hide();
            }
            else
            {
                if (!pane.IsShown()) pane.Show();
            }
        });
    }

    /// <summary>
    /// Transfers MainPage from pane proxy into a new floating window.
    /// Must run on the UI thread.
    /// </summary>
    private void ShowFloatingWindow()
    {
        if (_floatingWindow != null) return;

        try
        {
            _paneProxy.Content = null;
            _floatingWindow = Host.GetService<MainWindow>();
            _floatingWindow.ContentHost.Content = _mainPage;
            _floatingWindow.Closed += OnFloatingWindowClosed;
            _floatingWindow.SetHostAppOwner();
            _floatingWindow.Show();
        }
        catch (Exception ex)
        {
            logger.ZLogError($"ShowFloatingWindow failed: {ex.Message}");
            _floatingWindow = null;
            _paneProxy.Content = _mainPage;
        }
    }

    /// <summary>
    /// Transfers MainPage from floating window back into pane proxy.
    /// Must run on the UI thread.
    /// </summary>
    private void CloseFloatingWindow()
    {
        if (_floatingWindow is null) return;

        try
        {
            _floatingWindow.Closed -= OnFloatingWindowClosed;
            _floatingWindow.ContentHost.Content = null;
            _floatingWindow.Close();
        }
        catch (Exception ex)
        {
            logger.ZLogError($"CloseFloatingWindow failed: {ex.Message}");
        }
        finally
        {
            _floatingWindow = null;
            _paneProxy.Content = _mainPage;
        }
    }

    private void OnFloatingWindowClosed(object? sender, EventArgs e)
    {
        if (_floatingWindow == null) return;
        _floatingWindow.Closed -= OnFloatingWindowClosed;
        _floatingWindow.ContentHost.Content = null;
        _floatingWindow = null;
        _paneProxy.Content = _mainPage;
    }

    private bool TryGetDockablePane([NotNullWhen(true)] out DockablePane? pane)
    {
        pane = null;
        if (!_paneRegistered) return false;
        if (!DockablePane.PaneIsRegistered(PaneId)) return false;
        if (!DockablePane.PaneExists(PaneId)) return false;
        pane = new DockablePane(PaneId);
        return true;
    }
}
