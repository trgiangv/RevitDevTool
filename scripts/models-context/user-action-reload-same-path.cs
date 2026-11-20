using System;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;

[Transaction(TransactionMode.Manual)]
public class Command : IExternalCommand
{
    private const string LinkName = "Reload-Link";
    private const string WorkFolder = @"C:\Users\truon\source\repos\RevitDevTool\artifacts\models-context-work\2026";

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var host = commandData.Application.ActiveUIDocument.Document;
        var type = new FilteredElementCollector(host).OfClass(typeof(RevitLinkType)).Cast<RevitLinkType>()
            .FirstOrDefault(candidate => candidate.Name.StartsWith(LinkName, StringComparison.OrdinalIgnoreCase));
        if (type == null)
        {
            message = "No link type named " + LinkName + " in " + host.Title + ".";
            return Result.Failed;
        }

        var before = LinkDocument(host, type);
        var versionBefore = before == null ? string.Empty : Document.GetDocumentVersion(before).VersionGUID.ToString();
        var elementsBefore = before == null ? 0 : ModelElements(before);
        var path = ModelPathUtils.ConvertModelPathToUserVisiblePath(type.GetExternalFileReference().GetAbsolutePath());

        type.Reload();

        var after = LinkDocument(host, type);
        if (after == null)
        {
            message = "The link did not load after Reload.";
            return Result.Failed;
        }

        var versionAfter = Document.GetDocumentVersion(after).VersionGUID.ToString();
        var changed = ChangedCount(after, versionBefore);
        var json = "{" + string.Join(",", new[]
        {
            Pair("link", LinkName),
            Pair("path", path),
            Pair("previousPath", path),
            "\"versionBefore\":\"" + versionBefore + "\"",
            "\"versionAfter\":\"" + versionAfter + "\"",
            "\"elementsBefore\":" + elementsBefore,
            "\"changed\":" + changed
        }) + "}";
        var stepFile = Path.Combine(WorkFolder, "reload-step.json");
        File.WriteAllText(stepFile, json);

        message = string.Join(Environment.NewLine, new[]
        {
            "Reloaded " + LinkName + " from " + path,
            "version before " + versionBefore + ", after " + versionAfter,
            "elements before " + elementsBefore + ", after " + ModelElements(after) + ", changed " + changed,
            "wrote " + stepFile
        });
        return Result.Succeeded;
    }

    private static Document LinkDocument(Document host, RevitLinkType type)
    {
        return new FilteredElementCollector(host).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>()
            .Where(instance => instance.GetTypeId() == type.Id)
            .Select(instance => instance.GetLinkDocument())
            .FirstOrDefault(document => document != null);
    }

    private static int ModelElements(Document document)
    {
        return new FilteredElementCollector(document).WhereElementIsNotElementType()
            .Count(element => element.Category != null && element.Category.CategoryType == CategoryType.Model);
    }

    private static int ChangedCount(Document document, string versionBefore)
    {
        Guid guid;
        if (!Guid.TryParse(versionBefore, out guid))
        {
            return -1;
        }

        try
        {
            var difference = document.GetChangedElements(guid);
            if (!difference.AreDeletedElementIdsAvailable)
            {
                return -1;
            }

            return difference.GetCreatedElementIds().Count + difference.GetModifiedElementIds().Count + difference.GetDeletedElementIds().Count;
        }
        catch (Exception)
        {
            return -1;
        }
    }

    private static string Pair(string name, string value)
    {
        return "\"" + name + "\":\"" + value.Replace("\\", "\\\\") + "\"";
    }
}
