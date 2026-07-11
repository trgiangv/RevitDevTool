using System.ComponentModel;
using System.Text.Json;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Nice3point.Revit.Toolkit;
using RevitMcpToolSet.Utilities;

namespace RevitMcpToolSet.Resources;

[McpServerResourceType]
[Description("Live model resources returning JSON snapshots for tool chaining.")]
public static class ModelResources
{
    private static readonly JsonSerializerOptions IndentedOptions = new() { WriteIndented = true };

    [McpServerResource(
        UriTemplate = "revit://model/types",
        Name = "revit_model_types",
        Title = "Model Types",
        MimeType = "application/json")]
    [Description("Family types by category, MEP system types, view templates, and title blocks.")]
    public static string GetTypes()
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");

        var familyTypes = new FilteredElementCollector(doc)
            .WhereElementIsElementType()
            .OfClass(typeof(FamilySymbol))
            .Cast<FamilySymbol>()
            .OrderBy(s => s.Category?.Name)
            .ThenBy(s => s.FamilyName)
            .ThenBy(s => s.Name)
            .Select(s => new
            {
                id = s.Id.ToValue(),
                name = s.Name,
                family = s.FamilyName,
                category = s.Category?.Name ?? "",
            })
            .ToList();

        var mepSystemTypes = new FilteredElementCollector(doc)
            .OfClass(typeof(MEPSystemType))
            .Cast<MEPSystemType>()
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .Select(s => new
            {
                id = s.Id.ToValue(),
                name = s.Name,
                family = s switch
                {
                    MechanicalSystemType => "Mechanical System",
                    PipingSystemType => "Piping System",
                    _ => "Electrical System",
                },
                category = s.Category?.Name ?? "",
            })
            .ToList();

        var viewTemplates = new FilteredElementCollector(doc)
            .OfClass(typeof(View))
            .Cast<View>()
            .Where(v => v.IsTemplate)
            .OrderBy(v => v.Name)
            .Select(v => new
            {
                id = v.Id.ToValue(),
                name = v.Name,
                viewType = v.ViewType.ToString(),
            })
            .ToList();

        var titleBlocks = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_TitleBlocks)
            .WhereElementIsElementType()
            .Cast<FamilySymbol>()
            .OrderBy(s => s.FamilyName)
            .ThenBy(s => s.Name)
            .Select(s => new
            {
                id = s.Id.ToValue(),
                name = s.Name,
                family = s.FamilyName,
            })
            .ToList();

        return Serialize(new
        {
            familyTypes,
            mepSystemTypes,
            viewTemplates,
            titleBlocks,
        });
    }

    [McpServerResource(
        UriTemplate = "revit://model/levels",
        Name = "revit_model_levels",
        Title = "Model Levels",
        MimeType = "application/json")]
    [Description("Levels with elevations in feet and associated views.")]
    public static string GetLevels()
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");

        var viewsByLevel = new FilteredElementCollector(doc)
            .OfClass(typeof(View))
            .Cast<View>()
            .Where(v => !v.IsTemplate && v.GenLevel is not null)
            .GroupBy(v => v.GenLevel.Id.ToValue())
            .ToDictionary(
                g => g.Key,
                g => g.Select(v => new { id = v.Id.ToValue(), name = v.Name, viewType = v.ViewType.ToString() }).ToList());

        var levels = new FilteredElementCollector(doc)
            .OfClass(typeof(Level))
            .Cast<Level>()
            .OrderBy(l => l.Elevation)
            .Select(l => new
            {
                id = l.Id.ToValue(),
                name = l.Name,
                elevation = l.Elevation,
                associatedViews = viewsByLevel.TryGetValue(l.Id.ToValue(), out var views)
                    ? views
                    : [],
            })
            .ToList();

        return Serialize(new { levels });
    }

    [McpServerResource(
        UriTemplate = "revit://model/views",
        Name = "revit_model_views",
        Title = "Model Views",
        MimeType = "application/json")]
    [Description("Views and sheets with type, template, and on-sheet metadata.")]
    public static string GetViews()
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");

        var viewsOnSheets = BuildViewsOnSheetsMap(doc);

        var views = new FilteredElementCollector(doc)
            .OfClass(typeof(View))
            .Cast<View>()
            .Where(v => !v.IsTemplate)
            .OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
            .Select(v =>
            {
                var templateName = "";
                if (v.ViewTemplateId != ElementId.InvalidElementId)
                {
                    templateName = (doc.GetElement(v.ViewTemplateId) as View)?.Name ?? "";
                }

                viewsOnSheets.TryGetValue(v.Id.ToValue(), out var sheetIds);

                return new
                {
                    id = v.Id.ToValue(),
                    name = v.Name,
                    viewType = v.ViewType.ToString(),
                    isSheet = v is ViewSheet,
                    sheetNumber = v is ViewSheet sheet ? sheet.SheetNumber : (string?)null,
                    level = v.GenLevel?.Name,
                    template = string.IsNullOrEmpty(templateName) ? null : templateName,
                    onSheet = sheetIds is { Count: > 0 },
                    sheetIds = sheetIds ?? [],
                };
            })
            .ToList();

        return Serialize(new { views });
    }

    [McpServerResource(
        UriTemplate = "revit://model/worksets",
        Name = "revit_model_worksets",
        Title = "Model Worksets",
        MimeType = "application/json")]
    [Description("Worksets with editability, owner, and element counts.")]
    public static string GetWorksets()
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");

        if (!doc.IsWorkshared)
            return Serialize(new { worksharingEnabled = false, worksets = Array.Empty<object>() });

        var worksetTable = doc.GetWorksetTable();
        var activeWorksetId = worksetTable.GetActiveWorksetId();

        var worksets = new List<object>();
        foreach (var workset in new FilteredWorksetCollector(doc).ToWorksets())
        {
            var elementCount = new FilteredElementCollector(doc)
                .WherePasses(new ElementWorksetFilter(workset.Id))
                .GetElementCount();

            worksets.Add(new
            {
                id = workset.Id.IntegerValue,
                name = workset.Name,
                kind = workset.Kind.ToString(),
                owner = workset.Owner,
                isEditable = workset.IsEditable,
                isOpen = workset.IsOpen,
                isActive = workset.Id == activeWorksetId,
                elementCount,
            });
        }

        return Serialize(new { worksharingEnabled = true, worksets });
    }

    [McpServerResource(
        UriTemplate = "revit://model/links",
        Name = "revit_model_links",
        Title = "Model Links",
        MimeType = "application/json")]
    [Description("Revit links and CAD imports with paths and load state.")]
    public static string GetLinks()
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

        return Serialize(new { links });
    }

    [McpServerResource(
        UriTemplate = "revit://model/selection",
        Name = "revit_model_selection",
        Title = "Model Selection",
        MimeType = "application/json")]
    [Description("Currently selected elements — signals engineer intent.")]
    public static string GetSelection()
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        var uiDoc = RevitContext.ActiveUiDocument;

        if (uiDoc is null)
            return Serialize(new { count = 0, elements = Array.Empty<object>() });

        var elements = uiDoc.Selection.GetElementIds()
            .Select(id => doc.GetElement(id))
            .Where(e => e is not null)
            .Select(e => new
            {
                id = e!.Id.ToValue(),
                name = e.Name ?? "",
                category = e.Category?.Name ?? "",
                family = e is FamilyInstance fi ? fi.Symbol.FamilyName : "",
                type = e is FamilyInstance fi2 ? fi2.Symbol.Name : e.Name ?? "",
                level = e.LevelId != ElementId.InvalidElementId
                    ? (doc.GetElement(e.LevelId) as Level)?.Name ?? ""
                    : "",
            })
            .ToList();

        return Serialize(new { count = elements.Count, elements });
    }

    [McpServerResource(
        UriTemplate = "revit://model/grids",
        Name = "revit_model_grids",
        Title = "Model Grids",
        MimeType = "application/json")]
    [Description("Grid names, IDs, and geometry for reference.")]
    public static string GetGrids()
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");

        var grids = new FilteredElementCollector(doc)
            .OfClass(typeof(Grid))
            .Cast<Grid>()
            .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                object? geometry = null;
                var curve = g.Curve;
                if (curve is Line line)
                {
                    geometry = new
                    {
                        kind = "line",
                        start = ToPoint(line.GetEndPoint(0)),
                        end = ToPoint(line.GetEndPoint(1)),
                    };
                }
                else if (curve is not null)
                {
                    geometry = new { kind = curve.GetType().Name };
                }

                return new
                {
                    id = g.Id.ToValue(),
                    name = g.Name,
                    geometry,
                };
            })
            .ToList();

        return Serialize(new { grids });
    }

    private static string Serialize(object data)
        => JsonSerializer.Serialize(data, IndentedOptions);

    private static double[] ToPoint(XYZ point) => [point.X, point.Y, point.Z];

    private static Dictionary<long, List<long>> BuildViewsOnSheetsMap(Document doc)
    {
        var map = new Dictionary<long, List<long>>();

        foreach (var viewport in new FilteredElementCollector(doc).OfClass(typeof(Viewport)).Cast<Viewport>())
        {
            var viewId = viewport.ViewId.ToValue();
            var sheetId = viewport.OwnerViewId.ToValue();
            if (!map.TryGetValue(viewId, out var sheetIds))
            {
                sheetIds = [];
                map[viewId] = sheetIds;
            }

            sheetIds.Add(sheetId);
        }

        foreach (var instance in new FilteredElementCollector(doc).OfClass(typeof(ScheduleSheetInstance)).Cast<ScheduleSheetInstance>())
        {
            var scheduleId = instance.ScheduleId.ToValue();
            var sheetId = instance.OwnerViewId.ToValue();
            if (!map.TryGetValue(scheduleId, out var sheetIds))
            {
                sheetIds = [];
                map[scheduleId] = sheetIds;
            }

            sheetIds.Add(sheetId);
        }

        return map;
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
