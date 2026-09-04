using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Nice3point.Revit.Toolkit;
using RevitMcpToolSet.Utilities;

namespace RevitMcpToolSet.Tools;

[McpServerToolType]
[Description("Tools for creating and managing views, sheets, and viewports in Revit.")]
public static class ViewSheetTools
{
    [McpServerTool(Name = "revit_create_view", Title = "Create View", ReadOnly = false)]
    [Description("Creates a floor plan, section, or 3D view.")]
    public static object CreateView(
        [Description("View type: floor_plan, section, or 3d")] string viewType,
        [Description("Level name (required for floor_plan)")] string? levelName = null,
        [Description("Name for the new view")] string? viewName = null,
        [Description("View template name to apply (floor_plan and 3d)")] string? templateName = null,
        [Description("Section bounding box minimum [x, y, z]")] double[]? min = null,
        [Description("Section bounding box maximum [x, y, z]")] double[]? max = null,
        [Description("Section view direction angle in degrees (0=North/+Y, 90=West/-X)")] double? directionAngle = null,
        [Description("Section depth (defaults to width)")] double? depth = null,
        [Description("When false, creates a perspective 3D view; otherwise isometric")] bool? isBoundingBox = null)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        var normalizedType = NormalizeViewType(viewType);

        View view;
        using var tx = new Transaction(doc, "MCP: revit_create_view");
        tx.Start();

        view = normalizedType switch
        {
            "floor_plan" => CreateFloorPlanView(doc, levelName, viewName, templateName),
            "section" => CreateSectionView(doc, min, max, directionAngle, depth, viewName),
            "3d" => Create3DView(doc, viewName, templateName, isBoundingBox),
            _ => throw new McpException($"Unsupported view type '{viewType}'. Use floor_plan, section, or 3d."),
        };

        tx.Commit();
        return new { viewId = view.Id.ToValue(), viewName = view.Name };
    }

    [McpServerTool(Name = "revit_create_sheet", Title = "Create Sheet", ReadOnly = false)]
    [Description("Creates a new drawing sheet with an optional title block.")]
    public static object CreateSheet(
        [Description("Title block family type ID (defaults to first available)")] long? titleBlockId = null)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");

        var titleBlockElementId = ResolveTitleBlockId(doc, titleBlockId);

        using var tx = new Transaction(doc, "MCP: revit_create_sheet");
        tx.Start();
        var sheet = ViewSheet.Create(doc, titleBlockElementId);
        tx.Commit();

        return new { sheetId = sheet.Id.ToValue(), sheetNumber = sheet.SheetNumber };
    }

    [McpServerTool(Name = "revit_place_on_sheet", Title = "Place on Sheet", ReadOnly = false)]
    [Description("Places a view or schedule onto a sheet.")]
    public static object PlaceOnSheet(
        [Description("Sheet element ID")] long sheetId,
        [Description("View or schedule element ID")] long viewOrScheduleId,
        [Description("Position on sheet [x, y] in feet")] double[]? position = null)
    {
        if (position is not null && position.Length < 2)
            throw new McpException("position must have at least 2 values [x, y].");

        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        var sheet = doc.GetElement(sheetId.ToElementId()) as ViewSheet
            ?? throw new McpException($"Sheet {sheetId} not found.");

        var element = doc.GetElement(viewOrScheduleId.ToElementId())
            ?? throw new McpException($"Element {viewOrScheduleId} not found.");

        var placement = position is { Length: >= 2 }
            ? new XYZ(position[0], position[1], 0.0)
            : XYZ.Zero;

        using var tx = new Transaction(doc, "MCP: revit_place_on_sheet");
        tx.Start();

        long viewportId;
        if (element is ViewSchedule schedule)
        {
            var instance = ScheduleSheetInstance.Create(doc, sheet.Id, schedule.Id, placement);
            viewportId = instance.Id.ToValue();
        }
        else if (element is View view)
        {
            if (view.IsTemplate)
                throw new McpException("Cannot place a template view on a sheet.");
            var viewport = Viewport.Create(doc, sheet.Id, view.Id, placement);
            viewportId = viewport.Id.ToValue();
        }
        else
        {
            throw new McpException($"Element {viewOrScheduleId} is not a view or schedule.");
        }

        tx.Commit();
        RevitContext.ActiveUiDocument?.RequestViewChange(sheet);
        return new { viewportId };
    }

    [McpServerTool(Name = "revit_apply_view_template", Title = "Apply View Template", ReadOnly = false)]
    [Description("Applies a named view template to a view, or detaches when templateName is null.")]
    public static object ApplyViewTemplate(
        [Description("Target view element ID")] long viewId,
        [Description("View template name (null to detach)")] string? templateName = null)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        var view = doc.GetElement(viewId.ToElementId()) as View
            ?? throw new McpException($"View {viewId} not found.");
        if (view.IsTemplate)
            throw new McpException("Cannot apply a template to a template view.");

        using var tx = new Transaction(doc, "MCP: revit_apply_view_template");
        tx.Start();

        if (string.IsNullOrWhiteSpace(templateName))
        {
            view.ViewTemplateId = ElementId.InvalidElementId;
            tx.Commit();
            return new { applied = false };
        }

        var template = FindViewTemplate(doc, templateName!, view.ViewType)
            ?? throw new McpException($"View template '{templateName}' not found.");

        try
        {
            view.ViewTemplateId = template.Id;
            tx.Commit();
            return new { applied = true };
        }
        catch (Exception ex)
        {
            if (tx.HasStarted()) tx.RollBack();
            throw new McpException($"Failed to apply view template: {ex.Message}");
        }
    }

    [McpServerTool(Name = "revit_list_views", Title = "List Views", ReadOnly = true)]
    [Description("Lists views and optionally sheets and templates with metadata.")]
    public static object ListViews(
        [Description("Include drawing sheets in the result")] bool? includeSheets = null,
        [Description("Include view templates in the result")] bool? includeTemplates = null)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        var includeSheetViews = includeSheets ?? false;
        var includeTemplateViews = includeTemplates ?? false;

        var views = new FilteredElementCollector(doc).OfClass(typeof(View))
            .Cast<View>()
            .Where(v => ShouldIncludeView(v, includeSheetViews, includeTemplateViews))
            .OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
            .Select(v => new
            {
                id = v.Id.ToValue(),
                name = v.Name,
                type = GetViewListType(v),
                level = GetViewLevelName(doc, v),
                isTemplate = v.IsTemplate,
            })
            .ToList();

        return new { views };
    }

    [McpServerTool(Name = "revit_activate_view", Title = "Activate View", ReadOnly = false)]
    [Description("Activates (opens) a view in the Revit UI.")]
    public static object ActivateView(
        [Description("View element ID")] long viewId)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        var uiDoc = RevitContext.ActiveUiDocument ?? throw new McpException("No active UI document.");
        var view = doc.GetElement(viewId.ToElementId()) as View
            ?? throw new McpException($"View {viewId} not found.");
        if (view.IsTemplate)
            throw new McpException("Cannot activate a template view.");

        uiDoc.ActiveView = view;
        return new { activated = true };
    }

    private static string NormalizeViewType(string viewType)
    {
        if (string.IsNullOrWhiteSpace(viewType))
            throw new McpException("viewType is required.");

        var normalized = viewType.Trim().ToLowerInvariant().Replace(" ", "_");
        return normalized switch
        {
            "floorplan" or "floor-plan" => "floor_plan",
            "three_d" or "three-d" => "3d",
            _ => normalized,
        };
    }

    private static ViewPlan CreateFloorPlanView(
        Document doc,
        string? levelName,
        string? viewName,
        string? templateName)
    {
        if (string.IsNullOrWhiteSpace(levelName))
            throw new McpException("levelName is required for floor_plan views.");

        var level = new FilteredElementCollector(doc).OfClass(typeof(Level))
            .Cast<Level>()
            .FirstOrDefault(l => l.Name.Equals(levelName, StringComparison.OrdinalIgnoreCase))
            ?? throw new McpException($"Level '{levelName}' not found.");

        var viewFamilyType = GetViewFamilyType(doc, ViewFamily.FloorPlan)
            ?? throw new McpException("No FloorPlan view family type found.");

        var viewPlan = ViewPlan.Create(doc, viewFamilyType.Id, level.Id);
        viewPlan.Name = string.IsNullOrWhiteSpace(viewName)
            ? $"Floor Plan - {level.Name}"
            : viewName;

        if (!string.IsNullOrWhiteSpace(templateName))
        {
            var template = FindViewTemplate(doc, templateName!, ViewType.FloorPlan)
                ?? throw new McpException($"View template '{templateName}' not found or is not a FloorPlan template.");
            viewPlan.ViewTemplateId = template.Id;
        }

        return viewPlan;
    }

    private static ViewSection CreateSectionView(
        Document doc,
        double[]? min,
        double[]? max,
        double? directionAngle,
        double? depth,
        string? viewName)
    {
        if (min is null || min.Length < 3)
            throw new McpException("min must have 3 values [x, y, z] for section views.");
        if (max is null || max.Length < 3)
            throw new McpException("max must have 3 values [x, y, z] for section views.");
        if (!directionAngle.HasValue)
            throw new McpException("directionAngle is required for section views.");

        var minX = min[0];
        var minY = min[1];
        var minZ = min[2];
        var maxX = max[0];
        var maxY = max[1];
        var maxZ = max[2];

        if (maxX <= minX || maxY <= minY || maxZ <= minZ)
            throw new McpException("Max values must be greater than min values.");
        if (depth.HasValue && depth.Value <= 0)
            throw new McpException("Depth must be positive.");

        var viewFamilyType = GetViewFamilyType(doc, ViewFamily.Section)
            ?? throw new McpException("No Section view family type found.");

        var angleRad = directionAngle.Value * Math.PI / 180.0;
        var center = new XYZ((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2);
        var viewDir = new XYZ(-Math.Sin(angleRad), Math.Cos(angleRad), 0).Normalize();
        var rightDir = new XYZ(Math.Cos(angleRad), Math.Sin(angleRad), 0).Normalize();
        var upDir = XYZ.BasisZ;

        var transform = Transform.Identity;
        transform.Origin = center;
        transform.BasisX = rightDir;
        transform.BasisY = upDir;
        transform.BasisZ = viewDir;

        var width = Math.Sqrt(Math.Pow(maxX - minX, 2) + Math.Pow(maxY - minY, 2));
        var height = maxZ - minZ;
        var sectionDepth = depth ?? width;

        var boundingBox = new BoundingBoxXYZ
        {
            Transform = transform,
            Min = new XYZ(-width / 2, -height / 2, 0),
            Max = new XYZ(width / 2, height / 2, sectionDepth),
        };

        var viewSection = ViewSection.CreateSection(doc, viewFamilyType.Id, boundingBox);
        viewSection.Name = string.IsNullOrWhiteSpace(viewName)
            ? $"Section - {DateTime.Now:yyyy-MM-dd HH-mm-ss}"
            : viewName;
        return viewSection;
    }

    private static View3D Create3DView(
        Document doc,
        string? viewName,
        string? templateName,
        bool? isBoundingBox)
    {
        var viewFamilyType = GetViewFamilyType(doc, ViewFamily.ThreeDimensional)
            ?? throw new McpException("No 3D view family type found.");

        var view3d = isBoundingBox == false
            ? View3D.CreatePerspective(doc, viewFamilyType.Id)
            : View3D.CreateIsometric(doc, viewFamilyType.Id);

        view3d.Name = string.IsNullOrWhiteSpace(viewName)
            ? $"3D View - {DateTime.Now:yyyy-MM-dd HH-mm-ss}"
            : viewName;

        if (!string.IsNullOrWhiteSpace(templateName))
        {
            var template = FindViewTemplate(doc, templateName!, ViewType.ThreeD)
                ?? throw new McpException($"View template '{templateName}' not found or is not a 3D template.");
            view3d.ViewTemplateId = template.Id;
        }

        return view3d;
    }

    private static ElementId ResolveTitleBlockId(Document doc, long? titleBlockId)
    {
        if (titleBlockId is > 0)
        {
            var element = doc.GetElement(titleBlockId.Value.ToElementId());
            if (element is FamilySymbol)
                return titleBlockId.Value.ToElementId();
            throw new McpException($"Title block {titleBlockId} not found.");
        }

        var defaultId = doc.GetDefaultFamilyTypeId(new ElementId(BuiltInCategory.OST_TitleBlocks));
        if (defaultId != ElementId.InvalidElementId)
            return defaultId;

        var firstTitleBlock = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_TitleBlocks)
            .OfClass(typeof(FamilySymbol))
            .Cast<FamilySymbol>()
            .FirstOrDefault();

        if (firstTitleBlock is null)
            throw new McpException("No title block family types found.");

        return firstTitleBlock.Id;
    }

    private static ViewFamilyType? GetViewFamilyType(Document doc, ViewFamily family) =>
        new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType))
            .Cast<ViewFamilyType>()
            .FirstOrDefault(vft => vft.ViewFamily == family);

    private static View? FindViewTemplate(Document doc, string templateName, ViewType viewType) =>
        new FilteredElementCollector(doc).OfClass(typeof(View))
            .Cast<View>()
            .FirstOrDefault(v => v.IsTemplate
                && v.Name.Equals(templateName, StringComparison.OrdinalIgnoreCase)
                && v.ViewType == viewType);

    private static bool ShouldIncludeView(View view, bool includeSheets, bool includeTemplates)
    {
        if (view.ViewType is ViewType.Undefined or ViewType.Internal)
            return false;
        if (view is ViewSchedule)
            return false;
        if (view.IsTemplate)
            return includeTemplates;
        if (view is ViewSheet)
            return includeSheets;
        return view.Name is not null;
    }

    private static string GetViewListType(View view) => view switch
    {
        ViewSheet => "Sheet",
        _ when view.IsTemplate => "Template",
        _ => view.ViewType.ToString(),
    };

    private static string? GetViewLevelName(Document doc, View view)
    {
        if (view is ViewPlan viewPlan && viewPlan.GenLevel is not null)
            return viewPlan.GenLevel.Name;

        if (view.LevelId != ElementId.InvalidElementId)
            return (doc.GetElement(view.LevelId) as Level)?.Name;

        return null;
    }
}
