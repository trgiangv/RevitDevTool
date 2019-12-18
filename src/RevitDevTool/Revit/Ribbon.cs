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
    public class Ribbon : IExternalApplication
    {
        private static string _assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
        public Result OnStartup(UIControlledApplication application)
        {
            AppDomain.CurrentDomain.TypeResolve += CurrentDomain_TypeResolve;
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

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

            PushButtonData data = new PushButtonData("TraceOutput", "TraceOutput", Ribbon._assemblyPath, "RevitDevTool.Revit.Command.TraceCommand")
            {
                LargeImage = ImageUtils.GetResourceImage("Images/log.png"),
                LongDescription = "Display trace/debug data",
            };

            rvtRibbonPanel.AddItem(data);
        }

        private void AddDockable(UIControlledApplication application)
        {
            DockablePaneRegisterUtils.Register<TraceOutputPage>(Resource.TraceGuid, application);
        }

        private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            return GetAssembly(args.Name.Split(',').First());
        }

        private Assembly CurrentDomain_TypeResolve(object sender, ResolveEventArgs args)
        {
            return GetAssembly(args.Name.Split(',').First());
        }

        public Assembly GetAssembly(string name)
        {
            string path = Directory.GetFiles(Path.GetDirectoryName(_assemblyPath)).FirstOrDefault(x => x.Contains(name));
            if (!string.IsNullOrEmpty(path))
            {
                return Assembly.LoadFrom(path);
            }
            return null;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}
