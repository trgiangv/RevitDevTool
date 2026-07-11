using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Nice3point.Revit.Toolkit;
using RevitMcpToolSet.Utilities;
namespace RevitMcpToolSet.Tools;

[McpServerToolType]
[Description("Tools for managing external file links in the Revit document.")]
public static class LinkTools
{
    [McpServerTool(Name = "revit_list_links", Title = "List Linked Files", ReadOnly = true)]
    [Description("Lists all linked Revit models and CAD imports with file path and load status.")]
    public static object ListLinks()
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");

        var links = new List<object>();

        foreach (var linkType in new FilteredElementCollector(doc).OfClass(typeof(RevitLinkType)).Cast<RevitLinkType>())
        {
            links.Add(new
            {
                id = linkType.Id.ToValue(),
                name = linkType.Name,
                type = "Revit",
                path = GetExternalPath(doc, linkType.Id),
                loaded = IsRevitLinkLoaded(doc, linkType),
            });
        }

        foreach (var import in new FilteredElementCollector(doc).OfClass(typeof(ImportInstance)).Cast<ImportInstance>())
        {
            links.Add(new
            {
                id = import.Id.ToValue(),
                name = import.Name ?? "",
                type = "CAD",
                path = GetImportPath(doc, import),
                loaded = true,
            });
        }

        return new { links };
    }

    private static string GetExternalPath(Document doc, ElementId elementId)
    {
        try
        {
            var reference = ExternalFileUtils.GetExternalFileReference(doc, elementId);
            return ModelPathUtils.ConvertModelPathToUserVisiblePath(reference.GetAbsolutePath());
        }
        catch
        {
            return "";
        }
    }

    private static string GetImportPath(Document doc, ImportInstance import)
    {
        try
        {
            var typeId = import.GetTypeId();
            if (typeId != ElementId.InvalidElementId)
            {
                var path = GetExternalPath(doc, typeId);
                if (!string.IsNullOrEmpty(path))
                    return path;
            }

            return import.Name ?? "";
        }
        catch
        {
            return import.Name ?? "";
        }
    }

    private static bool IsRevitLinkLoaded(Document doc, RevitLinkType linkType)
    {
        try
        {
            return RevitLinkType.IsLoaded(doc, linkType.Id);
        }
        catch
        {
            return false;
        }
    }
}
