using System.Windows.Input;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.Decorators;
using Nice3point.Revit.Toolkit.External;
using RevitDevTool.Logging.Listeners;
using RevitDevTool.Utils;
using RevitDevTool.View;
using RevitDevTool.ViewModel;

namespace RevitDevTool.Commands;

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class TraceCommand : ExternalCommand, IExternalCommandAvailability
{
    public const string CommandName = "TraceLog";
    private static readonly Guid PaneGuid = new("43AE2B41-0BE6-425A-B27A-724B2CE17351");
    private static readonly DockablePaneId PaneId = new(PaneGuid);
    public static readonly Action TraceReceivedHandler = OnTraceReceived;

    private static bool IsForceHide { get; set; }
    internal static TraceLogViewModel? SharedViewModel { get; private set; }
    private static TraceLogWindow? FloatingWindow { get; set; }
    private static bool HasUiDocument => Context.UiApplication.HasActiveUiDocument();

    public override void Execute()
    {
        if (Keyboard.IsKeyDown(Key.LeftCtrl)
            || Keyboard.IsKeyDown(Key.RightCtrl))
        {
            if (HasUiDocument)
            {
                ExecutePaneVisibility();
            }
            else
            {
                ExecuteFloatingWindow();
            }
            return;
        }

        ExecuteLastCode();
    }

    private static void ExecuteFloatingWindow()
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

    private void ExecutePaneVisibility()
    {
        var dockablePane = UiApplication.GetDockablePane(PaneId);
        if (dockablePane.IsShown())
        {
            dockablePane.Hide();
            IsForceHide = true;
        }
        else
        {
            SharedViewModel ??= Host.GetService<TraceLogViewModel>();
            SharedViewModel.Subscribe();
            dockablePane.Show();
            IsForceHide = false;
        }
    }

    private static void ExecuteLastCode()
    {
        var codeExecuteVm = Host.GetService<CodeExecuteViewModel>();
        codeExecuteVm.ExecuteLastItem();
    }

    public static void RegisterDockablePane(UIControlledApplication application)
    {
        SharedViewModel = Host.GetService<TraceLogViewModel>();
        DockablePaneProvider
            .Register(application, PaneGuid, CommandName)
            .SetConfiguration(data =>
            {
                data.FrameworkElement = Host.GetService<TraceLogPage>();
                data.InitialState = new DockablePaneState
                {
                    MinimumWidth = 550,
                    MinimumHeight = 600,
                    DockPosition = DockPosition.Right,
                    TabBehind = DockablePanes.BuiltInDockablePanes.PropertiesPalette
                };
            });

        application.ControlledApplication.DocumentOpened += OnDocumentOpened;
        application.ControlledApplication.DocumentClosed += OnDocumentClosed;
        NotifyListener.TraceReceived += TraceReceivedHandler;
    }

    private static void OnTraceReceived()
    {
        if (HasUiDocument)
        {
            var dockablePane = Context.UiControlledApplication.GetDockablePane(PaneId);
            if (!dockablePane.IsShown() && !IsForceHide)
            {
                dockablePane.Show();
            }
            return;
        }

        NotifyListener.TraceReceived -= TraceReceivedHandler;

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
        if (!HasUiDocument) return;
        CloseFloatingWindow();

        var dockablePane = Context.UiControlledApplication.GetDockablePane(PaneId);

        if (IsForceHide)
        {
            NotifyListener.TraceReceived -= TraceReceivedHandler;
            SharedViewModel?.Dispose();
            dockablePane.Hide();
        }
        else
        {
            if (!dockablePane.IsShown())
            {
                dockablePane.Show();
            }
        }
    }

    private static void ShowFloatingWindow()
    {
        if (FloatingWindow != null) return;
        if (SharedViewModel is null) return;

        DispatcherHelper.RunOnMainThread(() =>
        {
            FloatingWindow = Host.GetService<TraceLogWindow>();
            FloatingWindow.Closed += OnFloatingWindowClosed;
            FloatingWindow.SetRevitOwner();
            FloatingWindow.Show();
        });
    }

    private static void CloseFloatingWindow()
    {
        if (FloatingWindow is null) return;

        DispatcherHelper.RunOnMainThread(() =>
        {
            FloatingWindow!.Closed -= OnFloatingWindowClosed;
            FloatingWindow.Close();
            FloatingWindow = null;
        });
    }

    private static void OnFloatingWindowClosed(object? sender, EventArgs e)
    {
        if (FloatingWindow == null) return;
        FloatingWindow.Closed -= OnFloatingWindowClosed;
        FloatingWindow = null;
        if (HasUiDocument) return;
        NotifyListener.TraceReceived += TraceReceivedHandler;
    }

    private static void OnDocumentClosed(object? sender, DocumentClosedEventArgs args)
    {
        if (HasUiDocument) return;

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