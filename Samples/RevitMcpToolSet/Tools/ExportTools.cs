using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Nice3point.Revit.Toolkit;
using RevitMcpToolSet.Data;
using RevitMcpToolSet.Utilities;
namespace RevitMcpToolSet.Tools;

[McpServerToolType]
[Description("Tools for exporting Revit content to external file formats.")]
public static class ExportTools
{
    [McpServerTool(Name = "revit_export_pdf", Title = "Export to PDF", ReadOnly = true)]
    [Description("Exports one or more views to PDF files in a specified output directory.")]
    public static object ExportPdf(
        [Description("View element IDs to export (optional, uses current view if empty)")] long[]? elementIds = null,
        [Description("Output directory path (optional, uses temp dir if empty)")] string? directoryPath = null,
        [Description("Export scope: ByViewList or CurrentView")] string exportMode = "CurrentView",
        [Description("Export in background")] bool exportInBackground = true)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");

        var outputDir = string.IsNullOrEmpty(directoryPath)
            ? Path.GetTempPath()
            : PathGuard.SanitizeDirectoryPath(directoryPath!);
        PathGuard.CreateDirectory(outputDir);

        var options = new PDFExportOptions
        {
            ExportQuality = PDFExportQualityType.DPI300,
            ZoomType = ZoomType.Zoom,
            ZoomPercentage = 100,
            PaperFormat = ExportPaperFormat.Default,
        };

        var mode = exportMode.Equals("ByViewList", StringComparison.OrdinalIgnoreCase)
            ? ExportScope.ByViewList : ExportScope.CurrentView;

        string resultPath;
        if (mode == ExportScope.ByViewList && elementIds is { Length: > 0 })
        {
            var viewIds = elementIds.Select(id => id.ToElementId()).ToList();
            var fileName = PathGuard.GenerateUniqueFilePath(outputDir, doc.Title, "pdf");
            options.FileName = Path.GetFileName(fileName);
            doc.Export(outputDir, viewIds, options);
            resultPath = fileName;
        }
        else
        {
            var activeView = RevitContext.ActiveView ?? throw new McpException("No active view.");
            var fileName = PathGuard.GenerateUniqueFilePath(outputDir, $"{doc.Title}_{activeView.Name}", "pdf");
            options.FileName = Path.GetFileName(fileName);
            doc.Export(outputDir, new List<ElementId> { activeView.Id }, options);
            resultPath = fileName;
        }

        return new { result = $"Exported to: {resultPath}" };
    }

    [McpServerTool(Name = "revit_export_image", Title = "Export to Image", ReadOnly = true)]
    [Description("Exports one or more views to image files (PNG, JPG, BMP, or TIFF).")]
    public static object ExportImage(
        [Description("View element IDs to export (optional)")] long[]? elementIds = null,
        [Description("Output directory path (optional)")] string? directoryPath = null,
        [Description("Image format: PNG, JPG, BMP, or TIFF")] ImageOutputFormat imageFormat = ImageOutputFormat.PNG,
        [Description("Export scope: ByViewList, CurrentView, or VisibleRegion")] ExportScope exportMode = ExportScope.CurrentView)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");

        var outputDir = string.IsNullOrEmpty(directoryPath)
            ? Path.GetTempPath()
            : PathGuard.SanitizeDirectoryPath(directoryPath!);
        PathGuard.CreateDirectory(outputDir);

        var imageExportFormat = imageFormat switch
        {
            ImageOutputFormat.JPG => ImageFileType.JPEGLossless,
            ImageOutputFormat.BMP => ImageFileType.BMP,
            ImageOutputFormat.TIFF => ImageFileType.TIFF,
            _ => ImageFileType.PNG,
        };

        var options = new ImageExportOptions
        {
            FilePath = Path.Combine(outputDir, doc.Title),
            HLRandWFViewsFileType = imageExportFormat,
            ShadowViewsFileType = imageExportFormat,
            ImageResolution = ImageResolution.DPI_150,
            PixelSize = 1024,
        };

        if (exportMode == ExportScope.VisibleRegion)
        {
            options.ExportRange = ExportRange.VisibleRegionOfCurrentView;
        }
        else if (exportMode == ExportScope.ByViewList && elementIds is { Length: > 0 })
        {
            options.ExportRange = ExportRange.SetOfViews;
            options.SetViewsAndSheets(elementIds.Select(id => id.ToElementId()).ToList());
        }
        else
        {
            options.ExportRange = ExportRange.CurrentView;
        }

        doc.ExportImage(options);
        return new { outcome = "Success", result = $"Images exported to: {outputDir}" };
    }

    [McpServerTool(Name = "revit_export_csv", Title = "Export Schedule to CSV", ReadOnly = true)]
    [Description("Exports a Revit schedule to a CSV file.")]
    public static object ExportCsv(
        [Description("Schedule element ID")] long scheduleId,
        [Description("Output directory path (optional)")] string? directoryPath = null)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        var schedule = doc.GetElement(scheduleId.ToElementId()) as ViewSchedule
            ?? throw new McpException($"Schedule {scheduleId} not found.");

        var outputDir = string.IsNullOrEmpty(directoryPath)
            ? Path.GetTempPath()
            : PathGuard.SanitizeDirectoryPath(directoryPath!);
        PathGuard.CreateDirectory(outputDir);

        var filePath = PathGuard.GenerateUniqueFilePath(outputDir, schedule.Name, "csv");
        var exportOptions = new ViewScheduleExportOptions
        {
            FieldDelimiter = ",",
            Title = true,
        };

        schedule.Export(outputDir, Path.GetFileName(filePath), exportOptions);
        return new { success = $"Schedule exported to: {filePath}" };
    }
}
