using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;

[Transaction(TransactionMode.Manual)]
public class Command : IExternalCommand
{
    private const string SourceView = "L3";
    private const string ViewName = "MC Template Locked";
    private const string TemplateName = "MC VG Locked";

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var document = commandData.Application.ActiveUIDocument.Document;
        var views = new FilteredElementCollector(document).OfClass(typeof(View)).Cast<View>().ToList();
        var existing = views.FirstOrDefault(view => !view.IsTemplate && view.Name == ViewName);
        if (existing != null && existing.ViewTemplateId != ElementId.InvalidElementId)
        {
            message = Report(document, existing, "already there");
            return Result.Succeeded;
        }

        var source = views.FirstOrDefault(view => !view.IsTemplate && view.ViewType == ViewType.FloorPlan && view.Name == SourceView);
        if (source == null)
        {
            message = "No floor plan named '" + SourceView + "' in " + document.Title + ".";
            return Result.Failed;
        }

        using (var transaction = new Transaction(document, "Model Context template view"))
        {
            transaction.Start();
            try
            {
                var duplicate = existing ?? (View)document.GetElement(source.Duplicate(ViewDuplicateOption.Duplicate));
                duplicate.Name = ViewName;
                duplicate.ViewTemplateId = ElementId.InvalidElementId;
                var template = duplicate.CreateViewTemplate();
                template.Name = Unique(views, TemplateName);
                template.SetNonControlledTemplateParameterIds(new List<ElementId>());
                duplicate.ViewTemplateId = template.Id;
                transaction.Commit();
                message = Report(document, duplicate, "created");
                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                if (transaction.HasStarted())
                {
                    transaction.RollBack();
                }

                message = "Template view failed: " + exception.Message;
                return Result.Failed;
            }
        }
    }

    private static string Unique(List<View> views, string name)
    {
        var taken = new HashSet<string>(views.Select(view => view.Name));
        var candidate = name;
        for (var index = 2; taken.Contains(candidate); index++)
        {
            candidate = name + " " + index;
        }

        return candidate;
    }

    private static string Report(Document document, View view, string state)
    {
        var template = document.GetElement(view.ViewTemplateId) as View;
        var controlled = template == null ? 0 : template.GetTemplateParameterIds().Count - template.GetNonControlledTemplateParameterIds().Count;
        return "View '" + view.Name + "' (id " + view.Id + ") " + state + ", template '" + (template == null ? "none" : template.Name)
            + "' (id " + view.ViewTemplateId + ") controls " + controlled + " parameters. Next: user-action-activate-view.cs with ViewName = \"" + view.Name + "\".";
    }
}
