using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.Decorators;
using Nice3point.Revit.Toolkit.External;
using RevitDevTool.View;
using RevitDevTool.ViewModel;

namespace RevitDevTool.Commands;

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class TraceCommand : ExternalCommand
{
    private const string CommandName = "TraceLog";
    private const string Guid = "43AE2B41-0BE6-425A-B27A-724B2CE17351";
    private static DockablePaneId PaneId { get; } = new(new Guid(Guid));
    private static bool IsForceHide { get; set; }

    public override void Execute()
    {
        try
        {
            var dockableWindow = UiApplication.GetDockablePane(PaneId);
            if (dockableWindow.IsShown())
            {
                dockableWindow.Hide();
                IsForceHide = true;
            }
            else
            {
                dockableWindow.Show();
                IsForceHide = false;
            }
        }
        catch (Exception e)
        {
            Autodesk.Revit.UI.TaskDialog.Show("Error", e.ToString());
        }
    }
    
    public static void RegisterDockablePane(UIControlledApplication application)
    {
        var frameworkElement = new TraceLog();
        RegisterPane(application, frameworkElement);
        SubscribeEvents(application, frameworkElement);
    }

    private static void RegisterPane(UIControlledApplication application, TraceLog frameworkElement)
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

    private static void SubscribeEvents(UIControlledApplication application, TraceLog frameworkElement)
    {
        application.DockableFrameVisibilityChanged += (_, args) =>
        {
            if (args.PaneId != PaneId) return;
            if (frameworkElement.DataContext is not TraceLogViewModel vm) return;

            if (args.DockableFrameShown)
                vm.Subcribe();
            else
                vm.Dispose();
        };
        application.ControlledApplication.DocumentOpened += (_, _) => OnDocumentOpened(application);
    }

    private static void OnDocumentOpened(UIControlledApplication application)
    {
        var dockableWindow = application.GetDockablePane(PaneId);
        
        if (IsForceHide)
            dockableWindow.Hide();
        else if (!dockableWindow.IsShown())
            dockableWindow.Show();
    }
}