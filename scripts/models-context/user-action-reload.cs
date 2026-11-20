using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;

[Transaction(TransactionMode.Manual)]
public class Command : IExternalCommand
{
    private const string LinkName = "Reload-Link";
    private const string WorkFolder = @"C:\Users\truon\source\repos\RevitDevTool\artifacts\models-context-work\2026";
    private const string FileName = "Reload-Link-small.rvt";
    private const bool Back = false;

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var host = commandData.Application.ActiveUIDocument.Document;
        var stepFile = Path.Combine(WorkFolder, "reload-step.json");
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
        var previousPath = ModelPathUtils.ConvertModelPathToUserVisiblePath(type.GetExternalFileReference().GetAbsolutePath());
        var path = Back ? PreviousPathOf(stepFile) : Path.Combine(WorkFolder, FileName);
        if (!File.Exists(path))
        {
            message = "No file " + path;
            return Result.Failed;
        }

        var watch = Stopwatch.StartNew();
        var result = type.LoadFrom(ModelPathUtils.ConvertUserVisiblePathToModelPath(path), new WorksetConfiguration());
        watch.Stop();

        var after = LinkDocument(host, type);
        if (after == null)
        {
            message = "The link did not load: " + result.LoadResult + ".";
            return Result.Failed;
        }

        var versionAfter = Document.GetDocumentVersion(after).VersionGUID.ToString();
        var changed = ChangedCount(after, versionBefore);
        var json = "{" + string.Join(",", new[]
        {
            Pair("link", LinkName),
            Pair("path", path),
            Pair("previousPath", previousPath),
            "\"versionBefore\":\"" + versionBefore + "\"",
            "\"versionAfter\":\"" + versionAfter + "\"",
            "\"elementsBefore\":" + elementsBefore,
            "\"changed\":" + changed
        }) + "}";
        File.WriteAllText(stepFile, json);

        message = string.Join(Environment.NewLine, new[]
        {
            "Reloaded " + LinkName + " from " + path + " in " + watch.ElapsedMilliseconds + " ms: " + result.LoadResult,
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

    private static string PreviousPathOf(string stepFile)
    {
        var match = Regex.Match(File.ReadAllText(stepFile), "\"previousPath\":\"((?:[^\"\\\\]|\\\\.)*)\"");
        return match.Success ? match.Groups[1].Value.Replace("\\\\", "\\") : string.Empty;
    }

    private static string Pair(string name, string value)
    {
        return "\"" + name + "\":\"" + value.Replace("\\", "\\\\") + "\"";
    }
}
