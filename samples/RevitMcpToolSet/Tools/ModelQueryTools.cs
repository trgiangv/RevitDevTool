using System.ComponentModel;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Nice3point.Revit.Toolkit;
using RevitMcpToolSet.Data;
using RevitMcpToolSet.Utilities;

namespace RevitMcpToolSet.Tools;

[McpServerToolType]
[Description("Model intelligence tools for querying and analyzing elements in the Revit model.")]
public static class ModelQueryTools
{
    private static readonly string[] DefaultFields = ["id", "category", "family", "type", "level", "name", "workset", "bbox"];

    [McpServerTool(Name = "revit_get_model_summary", Title = "Get Model Summary", ReadOnly = true, UseStructuredContent = true)]
    [Description("Returns a project overview: project info, category counts, warnings count, levels, phases, worksets, and links.")]
    public static object GetModelSummary()
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        var info = doc.ProjectInformation;

        var categories = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .ToElements()
            .GroupBy(e => e.Category?.Name ?? "(none)")
            .OrderBy(g => g.Key)
            .Select(g => new { name = g.Key, count = g.Count() })
            .ToList();

        var levels = new FilteredElementCollector(doc)
            .OfClass(typeof(Level))
            .Cast<Level>()
            .OrderBy(l => l.Elevation)
            .Select(l => new { id = l.Id.ToValue(), name = l.Name, elevation = l.Elevation })
            .ToList();

        var phases = new List<object>();
        foreach (Phase phase in doc.Phases)
            phases.Add(new { id = phase.Id.ToValue(), name = phase.Name });

        var worksets = new List<object>();
        try
        {
            foreach (var workset in new FilteredWorksetCollector(doc).ToWorksets())
            {
                worksets.Add(new { id = workset.Id.IntegerValue, name = workset.Name, kind = workset.Kind.ToString() });
            }
        }
        catch
        {
            // Worksharing disabled.
        }

        var links = CollectLinkSummaries(doc);
        var warnings = doc.GetWarnings();

        return new
        {
            project = new
            {
                name = info.Name,
                number = info.Number,
                address = info.Address,
                client = info.ClientName,
                title = doc.Title,
            },
            categories,
            warnings_count = warnings?.Count ?? 0,
            levels,
            phases,
            worksets,
            links,
        };
    }

    [McpServerTool(Name = "revit_find_elements", Title = "Find Elements", ReadOnly = true, UseStructuredContent = true)]
    [Description("Structured element search using composable FilterSpec filters.")]
    public static object FindElements(
        [Description("Composable filter specification")] FilterSpec? filters = null,
        [Description("Limit results to the current Revit selection")] bool selectedOnly = false,
        [Description("Include element types in results")] bool includeTypes = false,
        [Description("Include element instances in results")] bool includeInstances = true,
        [Description("Maximum number of results to return")] int maxResults = 500,
        [Description("Pagination offset — skip this many matches before returning")] int offset = 0,
        [Description("Fields to return: id, category, family, type, level, name, workset, bbox")] string[]? fields = null)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");

        if (!includeTypes && !includeInstances)
            throw new McpException("At least one of includeTypes or includeInstances must be true.");

        if (maxResults <= 0)
            maxResults = 500;
        if (offset < 0)
            offset = 0;

        var requestedFields = (fields is { Length: > 0 } ? fields : DefaultFields)
            .Select(f => f.ToLowerInvariant())
            .Distinct()
            .ToArray();

        var collector = FilterSpecBuilder.BuildCollector(doc, filters, selectedOnly, includeTypes, includeInstances);
        var allElements = collector.ToElements();
        var count = allElements.Count;
        var page = allElements.Skip(offset).Take(maxResults).ToList();
        var truncated = offset + page.Count < count;

        var elements = page.Select(e => ProjectElementFields(doc, e, requestedFields)).ToList();
        var structured = new { count, truncated, elements };
        return structured;
    }

    [McpServerTool(Name = "revit_read_parameters", Title = "Read Element Parameters", ReadOnly = true, UseStructuredContent = true)]
    [Description("Returns parameters for one or more elements. Omit paramNames to return all parameters.")]
    public static object ReadParameters(
        [Description("Element IDs to read")] long[] elementIds,
        [Description("Optional parameter names to include (null = all)")] string[]? paramNames = null)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        if (elementIds is not { Length: > 0 })
            throw new McpException("At least one element ID is required.");

        var nameFilter = paramNames is { Length: > 0 }
            ? new HashSet<string>(paramNames, StringComparer.OrdinalIgnoreCase)
            : null;

        var elements = new List<object>();
        foreach (var elementId in elementIds)
        {
            var element = doc.GetElement(elementId.ToElementId())
                ?? throw new McpException($"Element with ID {elementId} not found.");

            var parameters = new List<ParameterEntry>();
            foreach (Parameter param in element.Parameters)
            {
                var name = param.Definition.Name;
                if (nameFilter is not null && !nameFilter.Contains(name))
                    continue;

                parameters.Add(new ParameterEntry
                {
                    Name = name,
                    Value = ParameterAccessor.GetParameterValue(param),
                    Storage = param.StorageType.ToString(),
                    Writable = !param.IsReadOnly,
                    Builtin = param.Definition is InternalDefinition idef
                              && idef.BuiltInParameter != BuiltInParameter.INVALID,
                    IsShared = param.IsShared,
                    HasValue = param.HasValue,
                });
            }

            elements.Add(new { id = element.Id.ToValue(), @params = parameters });
        }

        return new { elements };
    }

    [McpServerTool(Name = "revit_list_types", Title = "List Types", ReadOnly = true)]
    [Description("Lists available types for family, MEP system, view template, or title block kinds.")]
    public static object ListTypes(
        [Description("Type kind: family, mep_system, view_template, or title_block")] string kind,
        [Description("Optional category name when kind is family")] string? category = null)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");

        var normalizedKind = kind.Trim().ToLowerInvariant();
        var types = normalizedKind switch
        {
            "family" => ListFamilyTypes(doc, category),
            "mep_system" => ListMepSystemTypes(doc),
            "view_template" => ListViewTemplates(doc),
            "title_block" => ListTitleBlockTypes(doc),
            _ => throw new McpException(
                $"Invalid kind '{kind}'. Use family, mep_system, view_template, or title_block."),
        };

        return new { types };
    }

    [McpServerTool(Name = "revit_list_category_parameters", Title = "List Category Parameters", ReadOnly = true)]
    [Description("Returns schedulable parameter names for a category using a temporary schedule definition.")]
    public static object ListCategoryParameters(
        [Description("Category display name, e.g. Doors or Rooms")] string categoryName)
    {
        if (string.IsNullOrWhiteSpace(categoryName))
            throw new McpException("Category name cannot be empty.");

        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        var category = FindCategory(doc, categoryName)
            ?? throw new McpException($"Category '{categoryName}' not found.");

        var sampleElement = new FilteredElementCollector(doc)
            .OfCategoryId(category.Id)
            .WhereElementIsNotElementType()
            .FirstElement();

        using var tx = new Transaction(doc, "Temporary Schedule for Parameter Discovery");
        tx.Start();
        try
        {
            var tempSchedule = ViewSchedule.CreateSchedule(doc, category.Id);
            var schedulableFields = tempSchedule.Definition.GetSchedulableFields();
            var parameters = schedulableFields
                .Select(sf =>
                {
                    var name = sf.GetName(doc);
                    var storageType = "Unknown";
                    var sampleValue = "";

                    if (sampleElement is not null)
                    {
                        var param = sampleElement.LookupParameter(name);
                        if (param is not null)
                        {
                            storageType = param.StorageType.ToString();
                            sampleValue = ParameterAccessor.GetParameterValue(param);
                        }
                    }

                    return new { name, storageType, sampleValue };
                })
                .OrderBy(p => p.name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new { parameters };
        }
        finally
        {
            if (tx.HasStarted())
                tx.RollBack();
        }
    }

    private static List<object> ListFamilyTypes(Document doc, string? categoryName)
    {
        var collector = new FilteredElementCollector(doc)
            .WhereElementIsElementType()
            .OfClass(typeof(FamilySymbol));

        if (!string.IsNullOrWhiteSpace(categoryName))
        {
            var category = FindCategory(doc, categoryName!)
                ?? throw new McpException($"Category '{categoryName}' not found.");
            collector = collector.OfCategoryId(category.Id);
        }

        return collector
            .Cast<FamilySymbol>()
            .OrderBy(s => s.FamilyName)
            .ThenBy(s => s.Name)
            .Select(s => (object)new
            {
                id = s.Id.ToValue(),
                name = s.Name,
                family = s.FamilyName,
                category = s.Category?.Name ?? "",
            })
            .ToList();
    }

    private static List<object> ListMepSystemTypes(Document doc)
    {
        return new FilteredElementCollector(doc)
            .OfClass(typeof(MEPSystemType))
            .Cast<MEPSystemType>()
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .Select(s => (object)new
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
    }

    private static List<object> ListViewTemplates(Document doc)
    {
        return new FilteredElementCollector(doc)
            .OfClass(typeof(View))
            .Cast<View>()
            .Where(v => v.IsTemplate)
            .OrderBy(v => v.Name)
            .Select(v => (object)new
            {
                id = v.Id.ToValue(),
                name = v.Name,
                family = "View Template",
                category = v.ViewType.ToString(),
            })
            .ToList();
    }

    private static List<object> ListTitleBlockTypes(Document doc)
    {
        return new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_TitleBlocks)
            .WhereElementIsElementType()
            .Cast<FamilySymbol>()
            .OrderBy(s => s.FamilyName)
            .ThenBy(s => s.Name)
            .Select(s => (object)new
            {
                id = s.Id.ToValue(),
                name = s.Name,
                family = s.FamilyName,
                category = s.Category?.Name ?? "",
            })
            .ToList();
    }

    private static Dictionary<string, object?> ProjectElementFields(Document doc, Element element, string[] fields)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in fields)
        {
            switch (field)
            {
                case "id":
                    result["id"] = element.Id.ToValue();
                    break;
                case "name":
                    result["name"] = element.Name ?? "";
                    break;
                case "category":
                    result["category"] = element.Category?.Name ?? "";
                    break;
                case "family":
                    result["family"] = element is FamilyInstance fi ? fi.Symbol.FamilyName : "";
                    break;
                case "type":
                    result["type"] = element is FamilyInstance fi2 ? fi2.Symbol.Name : element.Name ?? "";
                    break;
                case "level":
                    result["level"] = element.LevelId != ElementId.InvalidElementId
                        ? (doc.GetElement(element.LevelId) as Level)?.Name ?? ""
                        : "";
                    break;
                case "workset":
                    result["workset"] = GetWorksetName(doc, element);
                    break;
                case "bbox":
                    var bb = element.get_BoundingBox(null);
                    result["bbox"] = bb is null
                        ? null
                        : new Bounds3D
                        {
                            MinX = bb.Min.X, MinY = bb.Min.Y, MinZ = bb.Min.Z,
                            MaxX = bb.Max.X, MaxY = bb.Max.Y, MaxZ = bb.Max.Z,
                        };
                    break;
            }
        }

        return result;
    }

    private static string GetWorksetName(Document doc, Element element)
    {
        try
        {
            if (element.WorksetId == WorksetId.InvalidWorksetId)
                return "";

            return doc.GetWorksetTable().GetWorkset(element.WorksetId).Name;
        }
        catch
        {
            return "";
        }
    }

    private static Category? FindCategory(Document doc, string categoryName)
    {
        foreach (Category cat in doc.Settings.Categories)
        {
            if (cat.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase))
                return cat;
        }

        return null;
    }

    private static List<object> CollectLinkSummaries(Document doc)
    {
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

        return links;
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
