using System.ComponentModel;
using System.Text;
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
    private static readonly string[] DefaultExcelColumns = ["ElementId", "Name", "Category"];

    [McpServerTool(Name = "revit_export_pdf", Title = "Export to PDF", ReadOnly = true)]
    [Description("Exports one or more views to PDF files. Returns exported file paths and page count.")]
    public static object ExportPdf(
        [Description("View element IDs to export (null = active view)")] long[]? viewIds = null,
        [Description("Output directory path (null = temp directory)")] string? directory = null,
        [Description("When true, combine all views into a single PDF file")] bool? combineIntoSingle = null)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");

        var outputDir = string.IsNullOrWhiteSpace(directory)
            ? Path.GetTempPath()
            : PathGuard.SanitizeDirectoryPath(directory);
        PathGuard.CreateDirectory(outputDir);

        var resolvedViewIds = ResolveViewIds(doc, viewIds);
        var combine = combineIntoSingle ?? false;

        var options = new PDFExportOptions
        {
            ExportQuality = PDFExportQualityType.DPI300,
            ZoomType = ZoomType.Zoom,
            ZoomPercentage = 100,
            PaperFormat = ExportPaperFormat.Default,
            Combine = combine,
        };

        var existingFiles = SnapshotPdfFiles(outputDir);
        string? expectedCombinedPath = null;

        if (combine)
        {
            var fileName = Path.GetFileName(PathGuard.GenerateUniqueFilePath(outputDir, doc.Title, "pdf"));
            options.FileName = fileName;
            expectedCombinedPath = Path.Combine(outputDir, fileName);
        }

        if (!doc.Export(outputDir, resolvedViewIds, options))
            throw new McpException("PDF export failed for one or more views.");

        var filePaths = combine
            ? [expectedCombinedPath!]
            : FindNewPdfFiles(outputDir, existingFiles);

        if (filePaths.Count == 0)
            throw new McpException("PDF export completed but no output files were found.");

        return new
        {
            filePaths = filePaths.ToArray(),
            pageCount = resolvedViewIds.Count,
        };
    }

    [McpServerTool(Name = "revit_export_image", Title = "Export to Image", ReadOnly = true)]
    [Description("Exports one or more views to image files (png, jpg, or bmp).")]
    public static object ExportImage(
        [Description("View element IDs to export (null = active view)")] long[]? viewIds = null,
        [Description("Image format: png, jpg, or bmp")] string format = "png",
        [Description("Output directory path (null = temp directory)")] string? directory = null,
        [Description("Image resolution in DPI (default 150)")] int? resolution = null)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");

        var outputDir = string.IsNullOrWhiteSpace(directory)
            ? Path.GetTempPath()
            : PathGuard.SanitizeDirectoryPath(directory);
        PathGuard.CreateDirectory(outputDir);

        var resolvedViewIds = ResolveViewIds(doc, viewIds);
        var imageFormat = ParseImageFormat(format);

        var exportBase = Path.Combine(outputDir, $"export_{DateTime.Now:yyyyMMdd_HHmmss}");
        var existingFiles = SnapshotImageFiles(outputDir, exportBase);

        var options = new ImageExportOptions
        {
            ExportRange = ExportRange.SetOfViews,
            FilePath = exportBase,
            HLRandWFViewsFileType = imageFormat,
            ShadowViewsFileType = imageFormat,
            ImageResolution = MapDpi(resolution),
            ZoomType = ZoomFitType.FitToPage,
            PixelSize = 1024,
        };
        options.SetViewsAndSheets(resolvedViewIds);

        doc.ExportImage(options);

        var filePaths = FindNewImageFiles(outputDir, exportBase, existingFiles);
        if (filePaths.Count == 0)
            throw new McpException("Image export completed but no output files were found.");

        return new { filePaths = filePaths.ToArray() };
    }

    [McpServerTool(Name = "revit_export_to_excel", Title = "Export Elements to Spreadsheet", ReadOnly = true)]
    [Description("Exports element data matching FilterSpec filters to a CSV spreadsheet file.")]
    public static object ExportToExcel(
        [Description("Composable filter specification for element collection")] FilterSpec? filters = null,
        [Description("Parameter names to export (null = all available parameters)")] string[]? parameters = null,
        [Description("Output file path (null = auto-generated in temp directory)")] string? outputPath = null)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");

        var collector = FilterSpecBuilder.BuildCollector(doc, filters, selectedOnly: false, includeTypes: false, includeInstances: true);
        var elements = collector.ToElements();
        if (elements.Count == 0)
            throw new McpException("No elements match the specified filters.");

        var filePath = ResolveSpreadsheetOutputPath(doc.Title, outputPath);
        var rows = BuildElementRows(doc, elements, parameters, out var columns);

        WriteCsv(filePath, columns, rows);

        return new
        {
            filePath,
            rowCount = rows.Count,
            columnCount = columns.Count,
        };
    }

    [McpServerTool(Name = "revit_export_schedule", Title = "Export Schedule", ReadOnly = true)]
    [Description("Exports a Revit schedule to CSV (xlsx requests fall back to CSV).")]
    public static object ExportSchedule(
        [Description("Schedule element ID")] long scheduleId,
        [Description("Output format: csv or xlsx (xlsx falls back to CSV)")] string format = "csv",
        [Description("Output file path (null = auto-generated in temp directory)")] string? outputPath = null)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        var schedule = doc.GetElement(scheduleId.ToElementId()) as ViewSchedule
            ?? throw new McpException($"Schedule {scheduleId} not found.");

        var normalizedFormat = format.Trim().ToLowerInvariant();
        if (normalizedFormat is not ("csv" or "xlsx"))
            throw new McpException($"Invalid format '{format}'. Use 'csv' or 'xlsx'.");

        var filePath = ResolveScheduleOutputPath(schedule.Name, outputPath, normalizedFormat);
        var (columns, rows) = ReadScheduleTable(schedule);

        WriteCsv(filePath, columns, rows);

        return new
        {
            filePath,
            rowCount = rows.Count,
        };
    }

    private static IList<ElementId> ResolveViewIds(Document doc, long[]? viewIds)
    {
        if (viewIds is not { Length: > 0 })
        {
            var activeView = RevitContext.ActiveView ?? throw new McpException("No active view.");
            return [activeView.Id];
        }

        var resolved = new List<ElementId>();
        foreach (var viewId in viewIds)
        {
            var element = doc.GetElement(viewId.ToElementId())
                ?? throw new McpException($"View {viewId} not found.");
            if (element is not View view)
                throw new McpException($"Element {viewId} is not a view.");
            resolved.Add(view.Id);
        }

        return resolved;
    }

    private static ImageFileType ParseImageFormat(string format)
    {
        if (string.IsNullOrWhiteSpace(format))
            throw new McpException("Image format is required.");

        return format.Trim().ToLowerInvariant() switch
        {
            "png" => ImageFileType.PNG,
            "jpg" or "jpeg" => ImageFileType.JPEGLossless,
            "bmp" => ImageFileType.BMP,
            _ => throw new McpException($"Invalid image format '{format}'. Use 'png', 'jpg', or 'bmp'."),
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

    private static HashSet<string> SnapshotPdfFiles(string directory)
        => Directory.Exists(directory)
            ? Directory.GetFiles(directory, "*.pdf").ToHashSet(StringComparer.OrdinalIgnoreCase)
            : [];

    private static List<string> FindNewPdfFiles(string directory, HashSet<string> existingFiles)
        => Directory.GetFiles(directory, "*.pdf")
            .Where(path => !existingFiles.Contains(path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static HashSet<string> SnapshotImageFiles(string directory, string exportBase)
    {
        var prefix = Path.GetFileName(exportBase);
        return Directory.Exists(directory)
            ? Directory.GetFiles(directory, $"{prefix}*.*")
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
            : [];
    }

    private static List<string> FindNewImageFiles(string directory, string exportBase, HashSet<string> existingFiles)
    {
        var prefix = Path.GetFileName(exportBase);
        return Directory.GetFiles(directory, $"{prefix}*.*")
            .Where(path => !existingFiles.Contains(path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string ResolveSpreadsheetOutputPath(string baseName, string? outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
            return PathGuard.GenerateUniqueFilePath(Path.GetTempPath(), baseName, "csv");

        var filePath = PathGuard.SanitizeFilePath(outputPath);
        if (filePath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            filePath = Path.ChangeExtension(filePath, ".csv");

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
            PathGuard.CreateDirectory(directory);

        return filePath;
    }

    private static string ResolveScheduleOutputPath(string scheduleName, string? outputPath, string format)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
            return PathGuard.GenerateUniqueFilePath(Path.GetTempPath(), scheduleName, "csv");

        var filePath = PathGuard.SanitizeFilePath(outputPath);
        if (format == "xlsx" || filePath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            filePath = Path.ChangeExtension(filePath, ".csv");

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
            PathGuard.CreateDirectory(directory);

        return filePath;
    }

    private static List<Dictionary<string, string>> BuildElementRows(
        Document doc,
        IList<Element> elements,
        string[]? parameters,
        out List<string> columns)
    {
        var rows = new List<Dictionary<string, string>>();
        var columnSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (parameters is { Length: > 0 })
        {
            columns = DefaultExcelColumns.Concat(parameters).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var column in columns)
                columnSet.Add(column);

            foreach (var element in elements)
            {
                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ElementId"] = element.Id.ToValue().ToString(),
                    ["Name"] = element.Name ?? "",
                    ["Category"] = element.Category?.Name ?? "",
                };

                foreach (var paramName in parameters)
                {
                    if (string.IsNullOrWhiteSpace(paramName))
                        continue;
                    row[paramName] = GetParameterValue(doc, element, paramName);
                }

                rows.Add(row);
            }

            return rows;
        }

        foreach (var element in elements)
        {
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ElementId"] = element.Id.ToValue().ToString(),
                ["Name"] = element.Name ?? "",
                ["Category"] = element.Category?.Name ?? "",
            };

            CollectAllParameters(doc, element, row, columnSet);
            rows.Add(row);
        }

        columns = columnSet.OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList();
        return rows;
    }

    private static void CollectAllParameters(Document doc, Element element, Dictionary<string, string> row, HashSet<string> columnSet)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectParametersFromElement(element, row, columnSet, seen);
        CollectParametersFromElement(doc.GetElement(element.GetTypeId()), row, columnSet, seen);
    }

    private static void CollectParametersFromElement(
        Element? element,
        Dictionary<string, string> row,
        HashSet<string> columnSet,
        HashSet<string> seen)
    {
        if (element is null)
            return;

        foreach (Parameter param in element.Parameters)
        {
            var name = param.Definition?.Name;
            if (string.IsNullOrWhiteSpace(name) || !seen.Add(name))
                continue;

            columnSet.Add(name);
            row[name] = param.HasValue ? ParameterAccessor.GetParameterValue(param) : "";
        }
    }

    private static string GetParameterValue(Document doc, Element element, string parameterName)
    {
        var param = element.LookupParameter(parameterName);
        if (param is not null && param.HasValue)
            return ParameterAccessor.GetParameterValue(param);

        var typeElement = doc.GetElement(element.GetTypeId());
        param = typeElement?.LookupParameter(parameterName);
        return param is not null && param.HasValue
            ? ParameterAccessor.GetParameterValue(param)
            : "";
    }

    private static (List<string> Columns, List<Dictionary<string, string>> Rows) ReadScheduleTable(ViewSchedule schedule)
    {
        var tableData = schedule.GetTableData();
        var bodySection = tableData.GetSectionData(SectionType.Body);
        var headerSection = tableData.GetSectionData(SectionType.Header);

        var columnCount = bodySection.NumberOfColumns;
        if (columnCount <= 0)
            throw new McpException("Schedule has no columns to export.");

        var columns = new List<string>();
        if (headerSection.NumberOfRows > 0)
        {
            for (var col = 0; col < columnCount; col++)
                columns.Add(schedule.GetCellText(SectionType.Header, 0, col));
        }
        else
        {
            for (var col = 0; col < columnCount; col++)
                columns.Add($"Column_{col + 1}");
        }

        var rows = new List<Dictionary<string, string>>();
        for (var rowIndex = 0; rowIndex < bodySection.NumberOfRows; rowIndex++)
        {
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var col = 0; col < columnCount; col++)
                row[columns[col]] = schedule.GetCellText(SectionType.Body, rowIndex, col);
            rows.Add(row);
        }

        return (columns, rows);
    }

    private static void WriteCsv(string filePath, IReadOnlyList<string> columns, IReadOnlyList<Dictionary<string, string>> rows)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
            PathGuard.CreateDirectory(directory);

        var builder = new StringBuilder();
        builder.AppendLine(string.Join(",", columns.Select(EscapeCsv)));

        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(",", columns.Select(column =>
                EscapeCsv(row.TryGetValue(column, out var value) ? value : ""))));
        }

        File.WriteAllText(filePath, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private static string EscapeCsv(string? value)
    {
        value ??= "";
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
