using System.Windows;
using Autodesk.Revit.UI;
using Autodesk.Windows;
using DevTools.Utilities;
using UIFramework;
using TaskDialogResult = Autodesk.Revit.UI.TaskDialogResult;
// ReSharper disable ConvertToExtensionBlock

namespace RevitDevTool.Utils;

///<summary>
/// Provides extension methods for closing UI documents in the Revit application.
/// https://gist.github.com/ricaun/ff6814faf407ee044b93ee8e787f628c
/// </summary>
[PublicAPI]
public static class UiAppExtension
{
    /// <summary>
    /// Checks if there is an active UI document in the Revit application.
    /// </summary>
    /// <param name="uiapp">The UIApplication instance.</param>
    /// <returns>True if there is an active UI document; otherwise, false.</returns>
    public static bool HasActiveUiDocument(this UIApplication uiapp)
    {
        var frameManager = MainWindow.getMainWnd().frameManager;
        var activeFrameControl = frameManager.onGetActiveFrame();
        var activeFrameHost = activeFrameControl?.Content as MFCMDIFrameHost;
        return activeFrameHost?.document != null;
    }
    
    /// <summary>
    /// Closes all open UI documents in the Revit application.
    /// </summary>
    /// <param name="uiapp">The UIApplication instance.</param>
    /// <param name="saveModified">Indicates whether to save modified documents.</param>
    public static void CloseAllUiDocument(this UIApplication uiapp, bool saveModified = false)
    {
        using (new DialogBoxShowingForceResultYesNo(uiapp, saveModified))
        {
            foreach (var frameControl in MainWindow.getMainWnd().getAllViews())
            {
                frameControl.closeWindow();
            }
        }
    }

    /// <summary>
    /// Closes the active UI document in the Revit application.
    /// </summary>
    /// <param name="uiapp">The UIApplication instance.</param>
    /// <param name="saveModified">Indicates whether to save the modified document.</param>
    public static void CloseActiveUiDocument(this UIApplication uiapp, bool saveModified = false)
    {
        var frameManager = MainWindow.getMainWnd().frameManager;
        var activeFrameControl = frameManager.onGetActiveFrame();
        if (activeFrameControl is null) return;

        var activeFrameHost = activeFrameControl.Content as MFCMDIFrameHost;
        var activeDocument = activeFrameHost?.document;

        var allViews = frameManager.getAllMDIFrames();
        using (new DialogBoxShowingForceResultYesNo(uiapp, saveModified))
        {
            foreach (var frameControl in allViews)
            {
                var frameHost = frameControl.Content as MFCMDIFrameHost;
                if (frameHost?.document == activeDocument)
                {
                    frameControl.closeWindow();
                }
            }
        }
    }
    
    public static void SetRevitOwner(this Window window)
    {
        window.Owner = MainWindow.getMainWnd();
        window.Closed += (_, _) => Win32Utils.SetForegroundWindow(ComponentManager.ApplicationWindow);
    }

    /// <summary>
    /// Helper class to force a specific result for dialog boxes shown in the Revit application.
    /// </summary>
    private sealed class DialogBoxShowingForceResultYesNo : IDisposable
    {
        private readonly UIApplication _uiapp;
        private readonly bool _resultYes;

        /// <summary>
        /// Initializes a new instance of the <see cref="DialogBoxShowingForceResultYesNo"/> class.
        /// </summary>
        /// <param name="uiapp">The UIApplication instance.</param>
        /// <param name="resultYes">Indicates whether to force a 'Yes' result for dialog boxes.</param>
        public DialogBoxShowingForceResultYesNo(UIApplication uiapp, bool resultYes)
        {
            _uiapp = uiapp;
            _resultYes = resultYes;
            uiapp.DialogBoxShowing += OnDialogBoxShowing;
        }

        /// <summary>
        /// Event handler for the DialogBoxShowing event.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnDialogBoxShowing(object? sender, Autodesk.Revit.UI.Events.DialogBoxShowingEventArgs e)
        {
            var result = _resultYes ? TaskDialogResult.Yes : TaskDialogResult.No;
            e.OverrideResult((int)result);
        }

        /// <summary>
        /// Disposes the instance and unsubscribes from the DialogBoxShowing event.
        /// </summary>
        public void Dispose()
        {
            _uiapp.DialogBoxShowing -= OnDialogBoxShowing;
        }
    }
}