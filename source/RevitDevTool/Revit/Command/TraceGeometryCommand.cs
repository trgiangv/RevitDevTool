using Autodesk.Revit.UI;
using RevitDevTool.Utils;
using System.Diagnostics;
using System.Reflection;
using Autodesk.Revit.Attributes;

namespace RevitDevTool.Revit.Command;

[Transaction(TransactionMode.Manual)]
public class TraceGeometryCommand : IExternalCommand
{
    private static readonly TraceListener TraceListener = new TraceGeometryListener();
    private static bool _isRunning;
    public static readonly Dictionary<int, List<ElementId>> DocGeometries = new();
    
    private static void ChangIconStatus(UIApplication applicationData)
    {
        var panel = applicationData.GetRibbonPanels(Application.Name)
            .FirstOrDefault(x => x.Name == Application.Panel);
        var btn = panel?.GetItems().First(x => x.Name == typeof(TraceGeometryCommand).FullName) as RibbonButton;
        btn!.LargeImage = ImageUtils.GetResourceImage(_isRunning ? "Images/switch-on.png" : "Images/switch-off.png");
    }

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        if (_isRunning)
        {
            _isRunning = !_isRunning;
            Trace.Listeners.Remove(TraceListener);
        }
        else
        {
            TraceGeometryEventHandler.Instance.Start();
            _isRunning = !_isRunning;
            Trace.Listeners.Add(TraceListener);
        }

        ChangIconStatus(commandData.Application);

        return Result.Succeeded;
    }

    private static MethodInfo GenerateTransientDisplayMethod()
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
            
            var hashKey = doc.GetHashCode();
            if (DocGeometries.TryGetValue(hashKey, out var value))
            {
                value.Add(transientElementId);
            }
            else
            {
                DocGeometries[hashKey] = [transientElementId];
            }
        });
    }

    private class TraceGeometryListener : TraceListener
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