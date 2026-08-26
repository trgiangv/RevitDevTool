using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI.Selection;
using CSharpDemo.Extensions;
using Nice3point.Revit.Toolkit.External;
using System.Diagnostics;

namespace CSharpDemo;

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class CeilingVisualization : ExternalCommand
{
    public override void Execute()
    {
        var uiDoc = Application.ActiveUIDocument;
        var hostDoc = uiDoc.Document;
        var picks = uiDoc.Selection.PickObjects(ObjectType.LinkedElement, "Select ceiling(s)");
        if (picks.Count == 0)
            return;

        var linkInstance = hostDoc.GetElement(picks.First().ElementId) as RevitLinkInstance;
        if (linkInstance?.GetLinkDocument() is not { } ceilingDoc)
            return;

        var element = picks
            .Select(pick => ceilingDoc.GetElement(pick.LinkedElementId)).FirstOrDefault();
        // Trace.Write(element!.GetSolids(linkInstance.GetTotalTransform()));
        if (element is not Ceiling ceiling)
            return;

#if REVIT2025_OR_GREATER
        var curves = ceiling.GetCeilingGridLines(includeBoundary: true);
#else
        var curves = ceiling.GetCeilingGridLines(linkInstance, includeBoundary: true);
#endif
        Trace.Write(curves);
    }
}
