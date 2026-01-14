using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Windows;
using Nice3point.Revit.Toolkit.Decorators;
using Nice3point.Revit.Toolkit.External;
using RevitDevTool.Models.Trace;
using RevitDevTool.Utils;
using RevitDevTool.View;
using RevitDevTool.ViewModel;

namespace RevitDevTool.Commands;

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class TraceCommand : ExternalCommand, IExternalCommandAvailability
{
    public const string CommandName = "TraceLog";
    private const string Guid = "43AE2B41-0BE6-425A-B27A-724B2CE17351";
    public static readonly Action TraceReceivedHandler = OnTraceReceived;
    private static readonly DockablePaneId PaneId = new(new Guid(Guid));
    private static bool IsForceHide { get; set; }
    internal static TraceLogViewModel? SharedViewModel { get; private set; }
    private static TraceLogWindow? FloatingWindow { get; set; }
    private static bool HasUiDocumentOpened => Context.UiApplication.ActiveUIDocument is not null;

    public override void Execute()
    {
        try
        {
            if (!HasUiDocumentOpened)
            {
                if (FloatingWindow != null)
                {
                    CloseFloatingWindow();
                }
                else
                {
                    SharedViewModel ??= Host.GetService<TraceLogViewModel>();
                    SharedViewModel.Subscribe();
                    ShowFloatingWindow();
                }
            }

            var dockableWindow = UiApplication.GetDockablePane(PaneId);
            if (dockableWindow.IsShown())
            {
                dockableWindow.Hide();
                IsForceHide = true;
            }
            else
            {
                SharedViewModel ??= Host.GetService<TraceLogViewModel>();
                SharedViewModel.Subscribe();
                dockableWindow.Show();
                IsForceHide = false;
            }
        }
        catch (Exception e)
        {
            ErrorMessage = e.Message + "\n" + e.StackTrace;
        }
    }

    public static void RegisterDockablePane(UIControlledApplication application)
    {
        SharedViewModel = Host.GetService<TraceLogViewModel>();
        RegisterPane(application);
        SubscribeEvents(application);
    }

    private static void RegisterPane(UIControlledApplication application)
    {
        DockablePaneProvider
            .Register(application, new Guid(Guid), CommandName)
            .SetConfiguration(data =>
            {
                data.FrameworkElement = Host.GetService<TraceLogPage>();
                data.InitialState = CreateInitialState();
            });
    }

    private static DockablePaneState CreateInitialState()
    {
        return new DockablePaneState
        {
            MinimumWidth = 500,
            MinimumHeight = 400,
            DockPosition = DockPosition.Right,
            TabBehind = DockablePanes.BuiltInDockablePanes.PropertiesPalette
        };
    }

    private static void SubscribeEvents(UIControlledApplication application)
    {
        application.ControlledApplication.DocumentOpened += OnDocumentOpened;
        application.ControlledApplication.DocumentClosed += OnDocumentClosed;
        NotifyListener.TraceReceived += TraceReceivedHandler;
    }

    private static void OnTraceReceived()
    {
        NotifyListener.TraceReceived -= TraceReceivedHandler;

        if (HasUiDocumentOpened) return;
        if (SharedViewModel is not { IsStarted: true })
        {
            NotifyListener.TraceReceived += TraceReceivedHandler;
            return;
        }
        if (FloatingWindow != null) return;

        SharedViewModel.Subscribe();
        ShowFloatingWindow();
    }

    private static void OnDocumentOpened(object? sender, DocumentOpenedEventArgs args)
    {
        CloseFloatingWindow();
        NotifyListener.TraceReceived -= TraceReceivedHandler;

        if (IsForceHide)
        {
            SharedViewModel?.Dispose();
        }

        var dockablePane = Context.UiControlledApplication.GetDockablePane(PaneId);

        if (IsForceHide)
        {
            dockablePane.Hide();
        }
        else if (!dockablePane.IsShown())
        {
            dockablePane.Show();
        }
    }

    private static void ShowFloatingWindow()
    {
        if (FloatingWindow != null) return;
        if (SharedViewModel is null) return;

        // Ensure UI thread
        if (ComponentManager.IsApplicationButtonVisible)
        {
            ComponentManager.Ribbon.Dispatcher.BeginInvoke(new Action(Show));
        }
        else
        {
            Show();
        }

        return;

        static void Show()
        {
            FloatingWindow = Host.GetService<TraceLogWindow>();
            FloatingWindow.Closed += OnFloatingWindowClosed;
            FloatingWindow.SetRevitOwner();
            FloatingWindow.Show();
        }
    }

    private static void CloseFloatingWindow()
    {
        if (FloatingWindow is null) return;
        ComponentManager.Ribbon.Dispatcher.BeginInvoke(new Action(() =>
        {
            FloatingWindow.Closed -= OnFloatingWindowClosed;
            FloatingWindow.Close();
            FloatingWindow = null;
        }));
    }

    private static void OnFloatingWindowClosed(object? sender, EventArgs e)
    {
        if (FloatingWindow == null) return;
        FloatingWindow.Closed -= OnFloatingWindowClosed;
        FloatingWindow = null;
        if (HasUiDocumentOpened) return;
        NotifyListener.TraceReceived += TraceReceivedHandler;
    }

    private static void OnDocumentClosed(object? sender, DocumentClosedEventArgs args)
    {
        if (HasUiDocumentOpened) return;

        if (SharedViewModel is null or { IsStarted: false })
        {
            SharedViewModel = Host.GetService<TraceLogViewModel>();
        }

        NotifyListener.TraceReceived += TraceReceivedHandler;
    }

    public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
    {
        return true;
    }
}