using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using RevitMcpToolSet.Data;
namespace RevitMcpToolSet.Tools;

[McpServerToolType]
[Description("Tools for querying and analyzing elements in the Revit model.")]
public static class ModelQueryTools
{
    [McpServerTool(Name = "revit_find_elements", Title = "Find Elements", ReadOnly = true)]
    [Description("Searches Revit model elements with flexible filtering. Returns ElementSummary (Id, Name, Category) when BasicInfo is true, or full ElementDetail with parameters when false.")]
    public static object FindElements(
        [Description("Search configuration")] ElementSearchCriteria input)
    {
        var doc = Context.ActiveDocument ?? throw new McpException("No active document.");

        FilteredElementCollector collector;
        if (input.ViewId > 0)
            collector = new FilteredElementCollector(doc, new ElementId(input.ViewId));
        else if (input.SelectedOnly && Context.ActiveUiDocument is not null)
        {
            var selectedIds = Context.ActiveUiDocument.Selection.GetElementIds();
            collector = selectedIds.Count > 0
                ? new FilteredElementCollector(doc, selectedIds)
                : throw new McpException("No elements selected.");
        }
        else
            collector = new FilteredElementCollector(doc);

        if (!input.IncludeTypes)
            collector.WhereElementIsNotElementType();
        if (input.IncludeTypes && !input.IncludeInstances)
            collector.WhereElementIsElementType();

        if (input.Categories is { Length: > 0 })
        {
            var catIds = new List<ElementId>();
            foreach (var catName in input.Categories)
            {
                foreach (Category cat in doc.Settings.Categories)
                {
                    if (cat.Name.Equals(catName, StringComparison.OrdinalIgnoreCase))
                    {
                        catIds.Add(cat.Id);
                        break;
                    }
                }
            }
            if (catIds.Count > 0)
                collector = collector.WherePasses(new ElementMulticategoryFilter(catIds));
        }

        var elements = collector.ToList();

        if (input.FamilyNameFilters is { Length: > 0 })
            elements = elements.Where(e => input.FamilyNameFilters.Any(f =>
                e is FamilyInstance fi && fi.Symbol.FamilyName.Contains(f, StringComparison.OrdinalIgnoreCase))).ToList();

        if (input.ElementNameFilters is { Length: > 0 })
            elements = elements.Where(e => e.Name is not null && input.ElementNameFilters.Any(f =>
                e.Name.Contains(f, StringComparison.OrdinalIgnoreCase))).ToList();

        if (input.LevelNameFilters is { Length: > 0 })
            elements = elements.Where(e =>
            {
                var levelId = e.LevelId;
                if (levelId == ElementId.InvalidElementId) return false;
                var level = doc.GetElement(levelId) as Level;
                return level is not null && input.LevelNameFilters.Any(f =>
                    level.Name.Contains(f, StringComparison.OrdinalIgnoreCase));
            }).ToList();

        if (input.BoundingBox is not null)
        {
            var bb = input.BoundingBox;
            var outline = new Outline(new XYZ(bb.MinX, bb.MinY, bb.MinZ), new XYZ(bb.MaxX, bb.MaxY, bb.MaxZ));
            elements = elements.Where(e =>
            {
                var ebb = e.get_BoundingBox(null);
                if (ebb is null) return false;
                return input.BoundingBoxFilteringMode == SpatialFilterMode.ElementsInside
                    ? outline.Contains(ebb.Min, 0.01) && outline.Contains(ebb.Max, 0.01)
                    : new Outline(ebb.Min, ebb.Max).Intersects(outline, 0.01);
            }).ToList();
        }

        if (input.MaxResults > 0)
            elements = elements.Take(input.MaxResults).ToList();

        if (input.BasicInfo)
        {
            var results = elements.Select(e => new ElementSummary
            {
                Id = e.Id.Value,
                Name = e.Name ?? "",
                Category = e.Category?.Name ?? "",
            }).ToList();
            return new { outcome = $"Found {results.Count} elements", elements = results };
        }
        else
        {
            var results = elements.Select(e =>
            {
                var info = new ElementDetail
                {
                    Id = e.Id.Value,
                    Name = e.Name ?? "",
                    Category = e.Category?.Name ?? "",
                    ElementClass = e.GetType().Name,
                    TypeId = e.GetTypeId()?.Value ?? -1,
                };
                if (e is FamilyInstance fi)
                {
                    info.FamilyName = fi.Symbol.FamilyName;
                    info.TypeName = fi.Symbol.Name;
                }
                if (e.LevelId != ElementId.InvalidElementId)
                    info.LevelName = (doc.GetElement(e.LevelId) as Level)?.Name ?? "";

                var bb = e.get_BoundingBox(null);
                if (bb is not null)
                    info.BoundingBox = new Bounds3D
                    {
                        MinX = bb.Min.X, MinY = bb.Min.Y, MinZ = bb.Min.Z,
                        MaxX = bb.Max.X, MaxY = bb.Max.Y, MaxZ = bb.Max.Z,
                    };
                return info;
            }).ToList();
            return new { outcome = $"Found {results.Count} elements", elements = results };
        }
    }

    [McpServerTool(Name = "revit_group_elements_by", Title = "Group Elements By Property", ReadOnly = true)]
    [Description("Groups element details by a specified property (category, family, level, or type).")]
    public static object GroupElementsBy(
        [Description("Elements to group")] List<ElementDetail> elements,
        [Description("Group by: category, family, level, or type")] string groupBy)
    {
        var grouped = groupBy.ToLowerInvariant() switch
        {
            "category" => elements.GroupBy(e => e.Category),
            "family" => elements.GroupBy(e => e.FamilyName),
            "level" => elements.GroupBy(e => e.LevelName),
            "type" => elements.GroupBy(e => e.TypeName),
            _ => throw new McpException($"Invalid groupBy value: '{groupBy}'. Use category, family, level, or type."),
        };
        return new { result = grouped.ToDictionary(g => g.Key, g => g.ToList()) };
    }

    [McpServerTool(Name = "revit_analyze_model", Title = "Analyze Model Elements", ReadOnly = true)]
    [Description("Analyzes a collection of elements and returns a statistical breakdown by category, level, and family.")]
    public static object AnalyzeModel(
        [Description("Elements to analyze")] List<ElementDetail> elements)
    {
        return new
        {
            report = new ModelAnalysisReport
            {
                TotalElements = elements.Count,
                CategoryBreakdown = elements.GroupBy(e => e.Category).ToDictionary(g => g.Key, g => g.Count()),
                LevelDistribution = elements.Where(e => !string.IsNullOrEmpty(e.LevelName))
                    .GroupBy(e => e.LevelName).ToDictionary(g => g.Key, g => g.Count()),
                FamilyBreakdown = elements.Where(e => !string.IsNullOrEmpty(e.FamilyName))
                    .GroupBy(e => e.FamilyName).ToDictionary(g => g.Key, g => g.Count()),
                HasErrors = elements.Any(e => !string.IsNullOrEmpty(e.ErrorMessage)),
            },
        };
    }
}
