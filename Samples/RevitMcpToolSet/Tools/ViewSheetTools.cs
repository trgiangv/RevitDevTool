using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Nice3point.Revit.Toolkit;
using RevitMcpToolSet.Utilities;
namespace RevitMcpToolSet.Tools;

[McpServerToolType]
[Description("Tools for creating and managing views, sheets, and viewports in Revit.")]
public static class ViewSheetTools
{
    [McpServerTool(Name = "revit_create_floor_plan", Title = "Create Floor Plan", ReadOnly = false)]
    [Description("Creates a floor plan view for a given level.")]
    public static object CreateFloorPlan(
        [Description("Level name for the floor plan")] string levelName,
        [Description("Name for the new view (optional)")] string newName = "")
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");

        var level = new FilteredElementCollector(doc).OfClass(typeof(Level))
            .Cast<Level>().FirstOrDefault(l => l.Name.Equals(levelName, StringComparison.OrdinalIgnoreCase))
            ?? throw new McpException($"Level '{levelName}' not found.");

        var viewFamilyType = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType))
            .Cast<ViewFamilyType>().FirstOrDefault(vft => vft.ViewFamily == ViewFamily.FloorPlan)
            ?? throw new McpException("No FloorPlan view family type found.");

        using var tx = new Transaction(doc, "Create Floor Plan");
        tx.Start();
        var viewPlan = ViewPlan.Create(doc, viewFamilyType.Id, level.Id);
        viewPlan.Name = string.IsNullOrEmpty(newName) ? $"Auto-Floor Plan - {level.Name}" : newName;
        tx.Commit();

        RevitContext.ActiveUiDocument?.RequestViewChange(viewPlan);
        return new { elementId = viewPlan.Id.ToValue().ToString() };
    }

    [McpServerTool(Name = "revit_create_floor_plan_templated", Title = "Create Floor Plan with Template", ReadOnly = false)]
    [Description("Creates a floor plan view for a level and applies a view template to it.")]
    public static object CreateFloorPlanTemplated(
        [Description("Level name")] string levelName,
        [Description("View template name (optional)")] string viewTemplateName = "")
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");

        var level = new FilteredElementCollector(doc).OfClass(typeof(Level))
            .Cast<Level>().FirstOrDefault(l => l.Name.Equals(levelName, StringComparison.OrdinalIgnoreCase))
            ?? throw new McpException($"Level '{levelName}' not found.");

        var viewFamilyType = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType))
            .Cast<ViewFamilyType>().FirstOrDefault(vft => vft.ViewFamily == ViewFamily.FloorPlan)
            ?? throw new McpException("No FloorPlan view family type found.");

        View? template = null;
        if (!string.IsNullOrEmpty(viewTemplateName))
        {
            template = new FilteredElementCollector(doc).OfClass(typeof(View))
                .Cast<View>().FirstOrDefault(v => v.IsTemplate && v.Name.Equals(viewTemplateName, StringComparison.OrdinalIgnoreCase)
                    && v.ViewType == ViewType.FloorPlan)
                ?? throw new McpException($"View template '{viewTemplateName}' not found or is not a FloorPlan template.");
        }

        using var tx = new Transaction(doc, "Create Floor Plan with Template");
        tx.Start();
        var viewPlan = ViewPlan.Create(doc, viewFamilyType.Id, level.Id);
        viewPlan.Name = template is not null
            ? $"Floor Plan - {level.Name} ({template.Name})"
            : $"Auto-Floor Plan - {level.Name}";
        if (template is not null)
            viewPlan.ViewTemplateId = template.Id;
        tx.Commit();

        RevitContext.ActiveView = viewPlan;
        return new { elementId = viewPlan.Id.ToValue().ToString() };
    }

    [McpServerTool(Name = "revit_create_section", Title = "Create Section View", ReadOnly = false)]
    [Description("Creates a section view defined by a bounding box and view direction angle.")]
    public static object CreateSection(
        [Description("Minimum X of bounding box")] double minX,
        [Description("Minimum Y of bounding box")] double minY,
        [Description("Minimum Z of bounding box")] double minZ,
        [Description("Maximum X of bounding box")] double maxX,
        [Description("Maximum Y of bounding box")] double maxY,
        [Description("Maximum Z of bounding box")] double maxZ,
        [Description("View direction angle in degrees (0=North/+Y, 90=West/-X)")] double viewDirectionAngle,
        [Description("Custom view name (optional)")] string? viewName = null,
        [Description("Section depth (optional, defaults to width)")] double? depth = null)
    {
        if (maxX <= minX || maxY <= minY || maxZ <= minZ)
            throw new McpException("Max values must be greater than min values.");
        if (depth.HasValue && depth.Value <= 0)
            throw new McpException("Depth must be positive.");

        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");

        var viewFamilyType = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType))
            .Cast<ViewFamilyType>().FirstOrDefault(vft => vft.ViewFamily == ViewFamily.Section)
            ?? throw new McpException("No Section view family type found.");

        var angleRad = viewDirectionAngle * Math.PI / 180.0;
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

        var bbXyz = new BoundingBoxXYZ
        {
            Transform = transform,
            Min = new XYZ(-width / 2, -height / 2, 0),
            Max = new XYZ(width / 2, height / 2, sectionDepth),
        };

        using var tx = new Transaction(doc, "Create Section");
        tx.Start();
        var viewSection = ViewSection.CreateSection(doc, viewFamilyType.Id, bbXyz);
        viewSection.Name = viewName ?? $"Section - {DateTime.Now:yyyy-MM-dd HH-mm-ss}";
        tx.Commit();

        return new { elementId = viewSection.Id.ToValue().ToString() };
    }

    [McpServerTool(Name = "revit_create_sheet", Title = "Create Sheet", ReadOnly = false)]
    [Description("Creates a new drawing sheet with an optional title block.")]
    public static object CreateSheet(
        [Description("Title block family type ID (-1 for default)")] long titleBlockId = -1L)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");

        ElementId titleBlockElementId;
        if (titleBlockId > 0)
        {
            var element = doc.GetElement(titleBlockId.ToElementId());
            titleBlockElementId = element is FamilySymbol ? titleBlockId.ToElementId() : ElementId.InvalidElementId;
        }
        else
        {
            titleBlockElementId = doc.GetDefaultFamilyTypeId(new ElementId(BuiltInCategory.OST_TitleBlocks));
        }

        using var tx = new Transaction(doc, "Create Sheet");
        tx.Start();
        var sheet = ViewSheet.Create(doc, titleBlockElementId);
        tx.Commit();

        return new { sheetId = sheet.Id.ToValue().ToString() };
    }

    [McpServerTool(Name = "revit_place_view_on_sheet", Title = "Place View on Sheet", ReadOnly = false)]
    [Description("Places a view onto a sheet as a viewport at the specified position.")]
    public static object PlaceViewOnSheet(
        [Description("Sheet element ID")] long sheetId,
        [Description("View element ID")] long viewId,
        [Description("Position on sheet [X, Y, Z] (Z forced to 0)")] double[] viewPosition)
    {
        if (viewPosition.Length < 3) throw new McpException("viewPosition must have at least 3 values [X, Y, Z].");

        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        var sheet = doc.GetElement(sheetId.ToElementId()) as ViewSheet
            ?? throw new McpException($"Sheet {sheetId} not found.");
        var view = doc.GetElement(viewId.ToElementId()) as View
            ?? throw new McpException($"View {viewId} not found.");

        var position = new XYZ(viewPosition[0], viewPosition[1], 0.0);

        using var tx = new Transaction(doc, "Place View on Sheet");
        tx.Start();
        var viewport = Viewport.Create(doc, sheet.Id, view.Id, position);
        tx.Commit();

        RevitContext.ActiveUiDocument?.RequestViewChange(sheet);
        return new { viewportId = viewport.Id.ToValue().ToString() };
    }

    [McpServerTool(Name = "revit_apply_view_template", Title = "Apply View Template", ReadOnly = false)]
    [Description("Applies a named view template to a view.")]
    public static object ApplyViewTemplate(
        [Description("Target view element ID")] long viewId,
        [Description("View template name")] string viewTemplateName)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        var view = doc.GetElement(viewId.ToElementId()) as View
            ?? throw new McpException($"View {viewId} not found.");
        if (view.IsTemplate) throw new McpException("Cannot apply a template to a template view.");

        var template = new FilteredElementCollector(doc).OfClass(typeof(View))
            .Cast<View>().FirstOrDefault(v => v.IsTemplate && v.Name.Equals(viewTemplateName, StringComparison.OrdinalIgnoreCase))
            ?? throw new McpException($"View template '{viewTemplateName}' not found.");

        using var tx = new Transaction(doc, "Apply View Template");
        tx.Start();
        try
        {
            view.ViewTemplateId = template.Id;
            tx.Commit();
            return new { result = $"Applied template '{viewTemplateName}' to view '{view.Name}'." };
        }
        catch (Exception ex)
        {
            if (tx.HasStarted()) tx.RollBack();
            throw new McpException($"Failed to apply view template: {ex.Message}");
        }
    }

    [McpServerTool(Name = "revit_detach_view_template", Title = "Detach View Template", ReadOnly = false)]
    [Description("Removes the assigned view template from a view.")]
    public static object DetachViewTemplate(
        [Description("View element ID")] long viewId)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        var view = doc.GetElement(viewId.ToElementId()) as View
            ?? throw new McpException($"View {viewId} not found.");

        using var tx = new Transaction(doc, "Detach View Template");
        tx.Start();
        view.ViewTemplateId = ElementId.InvalidElementId;
        tx.Commit();
        return new { status = "Success" };
    }

    [McpServerTool(Name = "revit_activate_view", Title = "Activate View", ReadOnly = false)]
    [Description("Activates (opens) a view in the Revit UI.")]
    public static object ActivateView(
        [Description("View element ID")] long viewId)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        var view = doc.GetElement(viewId.ToElementId()) as View
            ?? throw new McpException($"View {viewId} not found.");
        if (view.IsTemplate) throw new McpException("Cannot activate a template view.");

        RevitContext.ActiveView = view;
        return new { outcome = "Success", viewId = view.Id.ToValue(), viewName = view.Name, viewType = view.ViewType.ToString() };
    }

    [McpServerTool(Name = "revit_list_views", Title = "List Views", ReadOnly = true)]
    [Description("Lists all non-template views in the document.")]
    public static object ListViews()
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        var activeViewId = doc.ActiveView?.Id;

        var views = new FilteredElementCollector(doc).OfClass(typeof(View))
            .Cast<View>()
            .Where(v => !v.IsTemplate
                && v is not ViewSchedule
                && v is not ViewSheet
                && v.ViewType != ViewType.Undefined
                && v.ViewType != ViewType.Internal
                && v.ViewType != ViewType.DrawingSheet
                && v.Name is not null)
            .OrderBy(v => v.Name)
            .Select(v => new
            {
                Id = v.Id.ToValue(),
                Name = v.Name,
                Type = v.ViewType.ToString(),
                IsActive = activeViewId is not null && v.Id == activeViewId,
            })
            .ToList();

        return new { views = JsonSerializer.Serialize(views) };
    }

    [McpServerTool(Name = "revit_list_sheets", Title = "List Sheets", ReadOnly = true)]
    [Description("Lists all drawing sheets in the document.")]
    public static object ListSheets()
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");

        var sheets = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet))
            .Cast<ViewSheet>()
            .Select(s => new { Id = s.Id.ToValue(), Name = s.Name, Number = s.SheetNumber })
            .ToList();

        return new { sheets = JsonSerializer.Serialize(sheets) };
    }

    [McpServerTool(Name = "revit_list_view_templates", Title = "List View Templates", ReadOnly = true)]
    [Description("Lists all view templates in the document.")]
    public static object ListViewTemplates()
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");

        var templates = new FilteredElementCollector(doc).OfClass(typeof(View))
            .Cast<View>()
            .Where(v => v.IsTemplate)
            .OrderBy(v => v.ViewType.ToString()).ThenBy(v => v.Name)
            .Select(v => new
            {
                Id = v.Id.ToValue(),
                Name = v.Name,
                ViewType = v.ViewType.ToString(),
                ViewFamily = GetViewFamilyName(v),
                Scale = v.Scale,
                DetailLevel = v.DetailLevel.ToString(),
                DisplayStyle = v.DisplayStyle.ToString(),
            })
            .ToList();

        return new { templates = JsonSerializer.Serialize(templates) };
    }

    [McpServerTool(Name = "revit_list_viewports", Title = "List Viewports on Sheet", ReadOnly = true)]
    [Description("Lists all viewports placed on a sheet.")]
    public static object ListViewports(
        [Description("Sheet element ID")] long sheetId)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        var sheet = doc.GetElement(sheetId.ToElementId()) as ViewSheet
            ?? throw new McpException($"Sheet {sheetId} not found.");

        var viewports = sheet.GetAllViewports()
            .Select(vpId =>
            {
                var vp = doc.GetElement(vpId) as Viewport;
                var center = vp?.GetBoxCenter();
                return new
                {
                    ViewportId = vpId.ToValue(),
                    ViewId = vp?.ViewId.ToValue(),
                    Center = center is not null ? new { X = center.X, Y = center.Y } : null,
                };
            })
            .ToList();

        return new { viewports = JsonSerializer.Serialize(viewports) };
    }

    [McpServerTool(Name = "revit_list_placed_views", Title = "List Placed Views on Sheet", ReadOnly = true)]
    [Description("Lists all views placed on a sheet with their names and types.")]
    public static object ListPlacedViews(
        [Description("Sheet element ID")] long sheetId)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        var sheet = doc.GetElement(sheetId.ToElementId()) as ViewSheet
            ?? throw new McpException($"Sheet {sheetId} not found.");

        var placedViews = sheet.GetAllViewports()
            .Select(vpId =>
            {
                var vp = doc.GetElement(vpId) as Viewport;
                var view = vp is not null ? doc.GetElement(vp.ViewId) as View : null;
                return new
                {
                    ViewportId = vpId.ToValue(),
                    ViewId = vp?.ViewId.ToValue(),
                    ViewName = view?.Name,
                    ViewType = view?.ViewType.ToString(),
                };
            })
            .ToList();

        return new { placedViews = JsonSerializer.Serialize(placedViews) };
    }

    [McpServerTool(Name = "revit_get_titleblock", Title = "Get Sheet Title Block", ReadOnly = true)]
    [Description("Returns the title block element ID for a given sheet.")]
    public static object GetTitleblock(
        [Description("Sheet element ID")] long sheetId)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        var sheet = doc.GetElement(sheetId.ToElementId()) as ViewSheet
            ?? throw new McpException($"Sheet {sheetId} not found.");

        var titleBlock = new FilteredElementCollector(doc, sheet.Id)
            .OfCategory(BuiltInCategory.OST_TitleBlocks)
            .FirstElement();
        return new { titleBlockId = titleBlock?.Id.ToValue().ToString() ?? "-1" };
    }

    [McpServerTool(Name = "revit_set_sheet_number", Title = "Set Sheet Number", ReadOnly = false)]
    [Description("Sets the sheet number for a drawing sheet.")]
    public static object SetSheetNumber(
        [Description("Sheet element ID")] long sheetId,
        [Description("New sheet number")] string sheetNumber)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        var sheet = doc.GetElement(sheetId.ToElementId()) as ViewSheet
            ?? throw new McpException($"Sheet {sheetId} not found.");

        using var tx = new Transaction(doc, "Set Sheet Number");
        tx.Start();
        sheet.SheetNumber = sheetNumber;
        tx.Commit();
        return new { };
    }

    [McpServerTool(Name = "revit_rename_sheet", Title = "Rename Sheet", ReadOnly = false)]
    [Description("Renames a drawing sheet.")]
    public static object RenameSheet(
        [Description("Sheet element ID")] long sheetId,
        [Description("New sheet name")] string newName)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        var sheet = doc.GetElement(sheetId.ToElementId()) as ViewSheet
            ?? throw new McpException($"Sheet {sheetId} not found.");

        using var tx = new Transaction(doc, "Rename Sheet");
        tx.Start();
        sheet.Name = newName;
        tx.Commit();
        return new { };
    }

    private static string GetViewFamilyName(View view) => view.ViewType switch
    {
        ViewType.FloorPlan => "FloorPlan",
        ViewType.CeilingPlan => "CeilingPlan",
        ViewType.Elevation => "Elevation",
        ViewType.ThreeD => "3D",
        ViewType.Section => "Section",
        ViewType.Detail => "Detail",
        ViewType.DraftingView => "Drafting",
        ViewType.Legend => "Legend",
        ViewType.Schedule => "Schedule",
        _ => view.ViewType.ToString(),
    };
}
