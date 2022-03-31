using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using RevitDevTool.Revit.Command;
using RevitDevTool.Theme;
using RevitDevTool.Utils;
using RevitDevTool.View;

namespace RevitDevTool.Revit
{
    public class Addin : IExternalApplication
    {
        private static string _assemblyPath = Assembly.GetExecutingAssembly().Location;
        public Result OnStartup(UIControlledApplication application)
        {
            LoadDependencies();

            AddButton(application);
            AddDockable(application);

            return Result.Succeeded;
        }

        private void LoadDependencies()
        {
            foreach (var depend in Directory.GetFiles(Path.GetDirectoryName(_assemblyPath)!, "*.dll"))
                Assembly.LoadFrom(depend);
        }

        private void AddButton(UIControlledApplication application)
        {

            RibbonPanel rvtRibbonPanel = null;
            if (application.GetRibbonPanels().Any(x => x.Name == "RevitDevTools"))
            {
                rvtRibbonPanel = application.GetRibbonPanels().Find(x => x.Name == "RevitDevTools");
            }
            else
            {
                rvtRibbonPanel = application.CreateRibbonPanel("RevitDevTools");
            }

            PushButtonData traceLogCmd = new PushButtonData("TraceLog", "TraceLog", Addin._assemblyPath, "RevitDevTool.Revit.Command.TraceCommand")
            {
                LargeImage = ImageUtils.GetResourceImage("Images/log.png"),
                LongDescription = "Display trace data",
            };

            PushButtonData traceGeometryCmd = new PushButtonData("TraceGeometry", "TraceGeometry", Addin._assemblyPath, "RevitDevTool.Revit.Command.TraceGeometryCommand")
            {
                LargeImage = ImageUtils.GetResourceImage("Images/switch-off.png"),
                LongDescription = "Trace geometries data",
            };

            PushButtonData clearCmd = new PushButtonData("ClearTraceGeometry", "ClearTraceGeometry", Addin._assemblyPath, "RevitDevTool.Revit.Command.ClearTraceGeometryCommand")
            {
                LargeImage = ImageUtils.GetResourceImage("Images/eraser.png"),
                LongDescription = "Clear current document trace geometries data",
            };

            rvtRibbonPanel.AddItem(traceLogCmd);
            rvtRibbonPanel.AddItem(traceGeometryCmd);
            rvtRibbonPanel.AddItem(clearCmd);
        }

        private void AddDockable(UIControlledApplication application)
        {
            DockablePaneRegisterUtils.Register<TraceLog>(Resource.TraceGuid, application);
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}
