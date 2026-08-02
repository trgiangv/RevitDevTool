using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Nice3point.Revit.Toolkit;
using RevitMcpToolSet.Utilities;

namespace RevitMcpToolSet.Tools;

/// <summary>
/// Demonstrates MCP SDK content blocks beyond plain JSON text:
/// inline images, embedded resources, resource links, and structured output.
/// </summary>
[McpServerToolType]
[Description("Multimodal MCP content demonstrations for vision, inline previews, and resource chaining.")]
public static class MultimodalContentTools
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [McpServerTool(Name = "revit_capture_view", Title = "Capture View (inline image)", ReadOnly = true)]
    [Description(
        "Captures the active view as inline PNG image content for vision models. " +
        "Prefer over revit_export_image when the agent needs immediate visual verification without a filesystem roundtrip.")]
    public static CallToolResult CaptureView(
        [Description("Image resolution in DPI (default 150)")] int? resolution = null) =>
        CaptureViewInternal(resolution);

    [McpServerTool(Name = "view_screenshot", Title = "View Screenshot", ReadOnly = true)]
    [Description(
        "Alias for revit_capture_view. Captures the active view as PNG image content for vision verification.")]
    public static CallToolResult ViewScreenshot(
        [Description("Image resolution in DPI (default 150)")] int? resolution = null) =>
        CaptureViewInternal(resolution);

    private static CallToolResult CaptureViewInternal(int? resolution)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        var view = RevitContext.ActiveView ?? throw new McpException("No active view.");

        var tempDir = Path.Combine(Path.GetTempPath(), "RevitMcpToolSet", Guid.NewGuid().ToString("N"));
        PathGuard.CreateDirectory(tempDir);

        try
        {
            var exportBase = Path.Combine(tempDir, "capture");
            var options = new ImageExportOptions
            {
                ExportRange = ExportRange.SetOfViews,
                FilePath = exportBase,
                HLRandWFViewsFileType = ImageFileType.PNG,
                ShadowViewsFileType = ImageFileType.PNG,
                ImageResolution = MapDpi(resolution),
                ZoomType = ZoomFitType.FitToPage,
                PixelSize = 1280,
            };
            options.SetViewsAndSheets([view.Id]);
            doc.ExportImage(options);

            var imagePath = Directory.GetFiles(tempDir, "capture*.*", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault()
                ?? throw new McpException("View capture completed but no image file was produced.");

            var imageBytes = File.ReadAllBytes(imagePath);
            return new CallToolResult { Content = [ImageContentBlock.FromBytes(imageBytes, "image/png")] };
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, recursive: true); }
                catch { /* best-effort temp cleanup */ }
            }
        }
    }

    [McpServerTool(Name = "revit_preview_schedule", Title = "Preview Schedule (embedded CSV)", ReadOnly = true)]
    [Description(
        "Returns a short text summary plus embedded CSV text resource for the first rows of a schedule. " +
        "Use for quick schedule QA without writing files or calling resources/read.")]
    public static CallToolResult PreviewSchedule(
        [Description("Schedule element ID")] long scheduleId,
        [Description("Maximum body rows to embed (default 30)")] int? maxRows = null)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        var schedule = doc.GetElement(scheduleId.ToElementId()) as ViewSchedule
            ?? throw new McpException($"Schedule {scheduleId} not found.");

        var (csv, embeddedRows, totalRows, columnCount) = SchedulePreviewHelper.BuildPreviewCsv(schedule, maxRows);
        var uri = $"revit://schedule/{scheduleId}/preview";

        return new CallToolResult
        {
            Content = [
                new TextContentBlock
                {
                    Text =
                        $"Schedule '{schedule.Name}' preview: {embeddedRows} of {totalRows} rows embedded as CSV."
                },
                new EmbeddedResourceBlock
                {
                    Resource = new TextResourceContents
                    {
                        Uri = uri,
                        MimeType = "text/csv",
                        Text = csv
                    }
                }
            ],
            StructuredContent = JsonSerializer.SerializeToElement(new
            {
                scheduleId,
                scheduleName = schedule.Name,
                embeddedRows,
                totalRows,
                columns = columnCount
            }, JsonOptions)
        };
    }

    [McpServerTool(
        Name = "revit_model_digest",
        Title = "Model Digest (resource link)",
        ReadOnly = true,
        UseStructuredContent = true)]
    [Description(
        "Compact project digest with structured counts and a resource link to revit://model/views for full view metadata. " +
        "Demonstrates text + ResourceLink + structuredContent for tool chaining.")]
    public static CallToolResult ModelDigest()
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");

        var viewCount = new FilteredElementCollector(doc).OfClass(typeof(View))
            .Cast<View>()
            .Count(v => !v.IsTemplate && v.ViewType is not ViewType.Undefined and not ViewType.Internal && v is not ViewSchedule);

        var levelCount = new FilteredElementCollector(doc).OfClass(typeof(Level)).GetElementCount();
        var warningCount = doc.GetWarnings()?.Count ?? 0;

        var structured = new
        {
            projectTitle = doc.Title,
            viewCount,
            levelCount,
            warningCount
        };

        return new CallToolResult
        {
            Content = [
                new TextContentBlock
                {
                    Text =
                        $"Project '{doc.Title}': {viewCount} views, {levelCount} levels, {warningCount} warnings. " +
                        "Use the linked resource for full view/sheet metadata."
                },
                new ResourceLinkBlock
                {
                    Uri = "revit://model/views",
                    Name = "revit_model_views",
                    Title = "Model views",
                    Description = "Full view and sheet metadata for documentation workflows.",
                    MimeType = "application/json"
                }
            ],
            StructuredContent = JsonSerializer.SerializeToElement(structured, JsonOptions)
        };
    }

    private static ImageResolution MapDpi(int? resolution)
    {
        var dpi = resolution ?? 150;
        return dpi switch
        {
            <= 72 => ImageResolution.DPI_72,
            <= 150 => ImageResolution.DPI_150,
            <= 300 => ImageResolution.DPI_300,
            _ => ImageResolution.DPI_600,
        };
    }

}
