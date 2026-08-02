using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Nice3point.Revit.Toolkit;
using RevitMcpToolSet.Utilities;

namespace RevitMcpToolSet.Resources;

[McpServerResourceType]
[Description("Parameterized element and schedule preview resources for invoke_dynamic batch reads.")]
public static class ElementResources
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    [McpServerResource(
        UriTemplate = "revit://element/{elementId}",
        Name = "revit_element",
        Title = "Element Summary",
        MimeType = "application/json")]
    [Description("Compact element summary: category, family/type, level, pinned, workset, bounding box.")]
    public static string GetElement([Description("Element ID")] long elementId)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        var element = doc.GetElement(elementId.ToElementId())
            ?? throw new McpException($"Element {elementId} not found.");

        string? family = null;
        string? typeName = null;
        if (element is FamilyInstance familyInstance)
        {
            family = familyInstance.Symbol.FamilyName;
            typeName = familyInstance.Symbol.Name;
        }

        string? levelName = null;
        if (element.LevelId != ElementId.InvalidElementId)
            levelName = (doc.GetElement(element.LevelId) as Level)?.Name;

        string? worksetName = null;
        if (doc.IsWorkshared && element.WorksetId != WorksetId.InvalidWorksetId)
        {
            try
            {
                worksetName = doc.GetWorksetTable().GetWorkset(element.WorksetId).Name;
            }
            catch
            {
                worksetName = null;
            }
        }

        object? boundingBox = null;
        var box = element.get_BoundingBox(null);
        if (box is not null)
            boundingBox = new
            {
                min = ToPoint(box.Min),
                max = ToPoint(box.Max),
            };

        return JsonSerializer.Serialize(new
        {
            id = element.Id.ToValue(),
            name = element.Name ?? "",
            category = element.Category?.Name ?? "",
            family,
            type = typeName,
            level = levelName,
            pinned = element.Pinned,
            workset = worksetName,
            boundingBox,
        }, JsonOptions);
    }

    [McpServerResource(
        UriTemplate = "revit://schedule/{scheduleId}/preview",
        Name = "revit_schedule_preview",
        Title = "Schedule Preview",
        MimeType = "text/csv")]
    [Description("CSV preview of schedule body rows (default 30 rows). Prefer over revit_preview_schedule when batching reads.")]
    public static string GetSchedulePreview([Description("Schedule element ID")] long scheduleId)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        var schedule = doc.GetElement(scheduleId.ToElementId()) as ViewSchedule
            ?? throw new McpException($"Schedule {scheduleId} not found.");

        var (csv, _, _, _) = SchedulePreviewHelper.BuildPreviewCsv(schedule);
        return csv;
    }

    private static double[] ToPoint(XYZ point) => [point.X, point.Y, point.Z];
}
