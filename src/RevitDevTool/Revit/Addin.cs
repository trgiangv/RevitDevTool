using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using RevitDevTool.Theme;
using RevitDevTool.Utils;
using RevitDevTool.View;

namespace RevitDevTool.Revit
{
    public class Addin : IExternalApplication
    {
        private static string _assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
        public Result OnStartup(UIControlledApplication application)
        {
            AppDomain.CurrentDomain.TypeResolve += CurrentDomain_Resolve;
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_Resolve;

            AddButton(application);
            AddDockable(application);

            return Result.Succeeded;
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

            PushButtonData data = new PushButtonData("TraceLog", "TraceLog", Addin._assemblyPath, "RevitDevTool.Revit.Command.TraceCommand")
            {
                LargeImage = ImageUtils.GetResourceImage("Images/log.png"),
                LongDescription = "Display trace data",
            };

            rvtRibbonPanel.AddItem(data);
        }

        private void AddDockable(UIControlledApplication application)
        {
            DockablePaneRegisterUtils.Register<TraceLog>(Resource.TraceGuid, application);
        }

        private Assembly CurrentDomain_Resolve(object sender, ResolveEventArgs args)
        {
            AssemblyName assemblyName = new AssemblyName(args.Name);
            if (assemblyName.Name.Split(',').First().EndsWith(".resources"))
            {
                return null;
            }
            
            string filePath = Path.Combine(Path.GetDirectoryName(_assemblyPath), $"{assemblyName.Name}.dll");
            return File.Exists(filePath) ? Assembly.LoadFile(filePath) : null;
        }


        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}
