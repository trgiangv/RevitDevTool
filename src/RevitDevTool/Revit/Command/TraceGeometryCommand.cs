using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitDevTool.Utils;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RevitDevTool.Revit.Command
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class TraceGeometryCommand : IExternalCommand
    {
        private static TraceListener _traceListener = new TraceGeometryListener();
        private static bool _isRunning = false;
        public static Dictionary<int, List<int>> DocGeometries = new Dictionary<int, List<int>>();
        public void ChangIconStatus(UIApplication applicationData)
        {
            var btn = applicationData.GetRibbonPanels().Find(x => x.Name == "RevitDevTools").GetItems().First(x => x.Name == "TraceGeometry") as RibbonButton;
            if (_isRunning)
            {
                btn.LargeImage = ImageUtils.GetResourceImage("Images/switch-on.png");
            }
            else
            {
                btn.LargeImage = ImageUtils.GetResourceImage("Images/switch-off.png");
            }
        }

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (_isRunning)
            {
                _isRunning = !_isRunning;
                Trace.Listeners.Remove(_traceListener);
                ChangIconStatus(commandData.Application);
            }
            else
            {
                TraceGeometryEventHandler.Instance.Start();
                _isRunning = !_isRunning;
                Trace.Listeners.Add(_traceListener);
                ChangIconStatus(commandData.Application);
            }

            return Result.Succeeded;
        }

        internal static MethodInfo GenerateTransientDisplayMethod()
        {
            var geometryElementType = typeof(GeometryElement);
            var geometryElementTypeMethods =
                geometryElementType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var method = geometryElementTypeMethods.FirstOrDefault(x => x.Name == "SetForTransientDisplay");
            return method;
        }

        private static void TraceGeometry(GeometryObject geometryObject)
        {
            TraceGeometry(new List<GeometryObject> { geometryObject });
        }

        private static void TraceGeometry(IEnumerable<GeometryObject> geometries)
        {
            TraceGeometryEventHandler.Instance.Invoke(app =>
            {
                var method = GenerateTransientDisplayMethod();
                var doc = app.ActiveUIDocument.Document;
                var argsM = new object[4];
                argsM[0] = doc;
                argsM[1] = ElementId.InvalidElementId;
                argsM[2] = geometries;
                argsM[3] = ElementId.InvalidElementId;
                var transientElementId = (ElementId)method.Invoke(null, argsM);

                int hashKey = doc.GetHashCode();
                if (DocGeometries.ContainsKey(hashKey))
                {
                    DocGeometries[hashKey].Add(transientElementId.IntegerValue);
                }
                else
                {
                    DocGeometries[hashKey] = new List<int>() { transientElementId.IntegerValue };
                }
            });
        }

        public class TraceGeometryListener : TraceListener
        {
            public override void Write(object o)
            {
                if (o is GeometryObject go)
                {
                    TraceGeometry(go);
                }
                if (o is IEnumerable<GeometryObject> geometries)
                {
                    TraceGeometry(geometries);
                }

                base.Write(o);
            }
            public override void Write(string message)
            {
            }

            public override void WriteLine(string message)
            {
            }
        }
    }
}
