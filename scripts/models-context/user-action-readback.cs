using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;

[Transaction(TransactionMode.Manual)]
public class Command : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var host = commandData.Application.ActiveUIDocument.Document;
        var lines = new List<string>
        {
            "Current model " + host.Title + ": " + ModelElements(host) + " model elements, modified " + host.IsModified
        };

        var instances = new FilteredElementCollector(host).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>().ToList();
        foreach (var type in new FilteredElementCollector(host).OfClass(typeof(RevitLinkType)).Cast<RevitLinkType>())
        {
            var path = ModelPathUtils.ConvertModelPathToUserVisiblePath(type.GetExternalFileReference().GetAbsolutePath());
            lines.Add("Link type " + type.Name + ": " + type.GetLinkedFileStatus() + ", " + path);
            foreach (var instance in instances.Where(candidate => candidate.GetTypeId() == type.Id))
            {
                var document = instance.GetLinkDocument();
                lines.Add(document == null
                    ? "  instance " + instance.Id + ": not loaded"
                    : "  instance " + instance.Id + ": " + document.Title + ", " + ModelElements(document)
                      + " model elements, version " + Document.GetDocumentVersion(document).VersionGUID);
            }
        }

        message = string.Join(Environment.NewLine, lines);
        return Result.Succeeded;
    }

    private static int ModelElements(Document document)
    {
        return new FilteredElementCollector(document).WhereElementIsNotElementType()
            .Count(element => element.Category != null && element.Category.CategoryType == CategoryType.Model);
    }
}
