using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;

[Transaction(TransactionMode.Manual)]
public class Command : IExternalCommand
{
    private const string TitlePrefix = "Snowdon Towers Sample HVAC";
    private const string PathContains = "";

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var application = commandData.Application;
        var current = application.ActiveUIDocument.Document;
        var open = application.Application.Documents.Cast<Document>()
            .Where(document => !document.IsLinked && !document.IsFamilyDocument && document.IsValidObject)
            .ToList();
        var target = open.FirstOrDefault(document => document != current
            && document.Title.StartsWith(TitlePrefix, StringComparison.OrdinalIgnoreCase)
            && document.PathName.IndexOf(PathContains, StringComparison.OrdinalIgnoreCase) >= 0);
        if (target == null)
        {
            message = "No other open document starts with '" + TitlePrefix + "'. Open: " + string.Join("; ", open.Select(document => document.Title));
            return Result.Failed;
        }

        if (string.IsNullOrEmpty(target.PathName))
        {
            message = target.Title + " has no path and cannot be activated by path.";
            return Result.Failed;
        }

        application.OpenAndActivateDocument(target.PathName);
        message = "Active document: " + current.Title + " -> " + application.ActiveUIDocument.Document.Title;
        return Result.Succeeded;
    }
}
