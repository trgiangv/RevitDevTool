using System.Windows;
using System.Windows.Interop;
using DevTools.Utilities;
namespace AcadDevTool.Utils;

public static class WindowUtils
{
    public static void SetAcadOwner(this Window window)
    {
        var acadHandle = Autodesk.AutoCAD.ApplicationServices.Core.Application.MainWindow.Handle;
        new WindowInteropHelper(window).Owner = acadHandle;
        window.Closed += (_, _) => Win32Utils.SetForegroundWindow(acadHandle);
    }
}