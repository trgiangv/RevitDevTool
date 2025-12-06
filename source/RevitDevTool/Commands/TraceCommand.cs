using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.Decorators;
using Nice3point.Revit.Toolkit.External;
using RevitDevTool.Models.Trace;
using RevitDevTool.View;
using RevitDevTool.ViewModel;
using ricaun.Revit.UI;

namespace RevitDevTool.Commands;

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class TraceCommand : ExternalCommand
{
    public const string CommandName = "TraceLog";
    private const string Guid = "43AE2B41-0BE6-425A-B27A-724B2CE17351";
    private static readonly Action TraceReceivedHandler = OnTraceReceived;
    private static readonly DockablePaneId PaneId = new(new Guid(Guid));
    private static bool IsForceHide { get; set; }
    private static TraceLogViewModel? SharedViewModel { get; set; }
    public static TraceLogWindow? FloatingWindow { get; private set; }
    private static bool HasDocumentOpend => Context.Application.Documents.Size > 0;

    public override void Execute()
    {
        try
        {
            if (!HasDocumentOpend)
            {
                if (FloatingWindow != null)
                {
                    CloseFloatingWindow();
                }
                else
                {
                    SharedViewModel ??= new TraceLogViewModel();
                    SharedViewModel.Subcribe();
                    ShowFloatingWindow();
                }
                return;
            }
            
            var dockableWindow = UiApplication.GetDockablePane(PaneId);
            if (dockableWindow.IsShown())
            {
                dockableWindow.Hide();
                IsForceHide = true;
            }
            else
            {
                SharedViewModel ??= new TraceLogViewModel();
                SharedViewModel.Subcribe();
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
        SharedViewModel = new TraceLogViewModel();
        var frameworkElement = new TraceLogPage(SharedViewModel);
        RegisterPane(application, frameworkElement);
        SubscribeEvents(application);
    }

    private static void RegisterPane(UIControlledApplication application, TraceLogPage frameworkElement)
    {
        DockablePaneProvider
            .Register(application, new Guid(Guid), CommandName)
            .SetConfiguration(data =>
            {
                data.FrameworkElement = frameworkElement;
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
        application.DockableFrameVisibilityChanged += (_, args) =>
        {
            if (args.PaneId != PaneId) return;
            if (SharedViewModel is null) return;

            if (args.DockableFrameShown)
            {
                SharedViewModel.Subcribe();
            }
            else
            {
                if (FloatingWindow is null)
                    SharedViewModel.Dispose();
            }
        };
        
        application.ControlledApplication.DocumentOpened += OnDocumentOpened;
        application.ControlledApplication.DocumentClosed += OnDocumentClosed;
        TraceEventNotifier.TraceReceived += TraceReceivedHandler;
    }
    
    private static void OnTraceReceived()
    {
        TraceEventNotifier.TraceReceived -= TraceReceivedHandler;
        
        if (HasDocumentOpend)
        {
            return;
        }
        
        if (SharedViewModel is not { IsStarted: true })
        {
            TraceEventNotifier.TraceReceived += TraceReceivedHandler;
            return;
        }
        
        if (FloatingWindow != null)
        {
            return;
        }
        
        SharedViewModel.Subcribe();
        ShowFloatingWindow();
    }

    private static void OnDocumentOpened(object? sender, DocumentOpenedEventArgs args)
    {
        CloseFloatingWindow();
        TraceEventNotifier.TraceReceived -= TraceReceivedHandler;
        
        if (IsForceHide)
        {
            SharedViewModel?.Dispose();
        }
        
        var dockableWindow = Context.UiControlledApplication.GetDockablePane(PaneId);
        
        if (IsForceHide)
            dockableWindow.Hide();
        else if (!dockableWindow.IsShown())
            dockableWindow.Show();
    }
    
    private static void ShowFloatingWindow()
    {
        if (FloatingWindow != null) return;
        if (SharedViewModel is null) return;
        FloatingWindow = new TraceLogWindow(SharedViewModel);
        FloatingWindow.Closed += OnFloatingWindowClosed;
        FloatingWindow.SetAutodeskOwner();
        FloatingWindow.Show();
    }
    
    private static void CloseFloatingWindow()
    {
        if (FloatingWindow is null) return;
        FloatingWindow.Closed -= OnFloatingWindowClosed;
        FloatingWindow.Close();
        FloatingWindow = null;
    }
    
    private static void OnFloatingWindowClosed(object? sender, EventArgs e)
    {
        if (FloatingWindow == null) return;
        FloatingWindow.Closed -= OnFloatingWindowClosed;
        FloatingWindow = null;
        if (HasDocumentOpend) return;
        TraceEventNotifier.TraceReceived += TraceReceivedHandler;
    }
    
    private static void OnDocumentClosed(object? sender, DocumentClosedEventArgs args)
    {
        if (HasDocumentOpend) return;
        
        if (SharedViewModel is null or { IsStarted: false })
        {
            SharedViewModel = new TraceLogViewModel();
        }
        
        TraceEventNotifier.TraceReceived += TraceReceivedHandler;
    }
}