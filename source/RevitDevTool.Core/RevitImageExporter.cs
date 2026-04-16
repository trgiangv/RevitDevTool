using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text;

namespace RevitDevTool.Core;

/// <summary>
/// Exports Revit views as images for AI agent consumption.
/// </summary>
/// <example>
/// <code>
/// // Export active view as base64 PNG:
/// var result = RevitImageExporter.ExportActiveView();
///
/// // Export a specific view by name:
/// var result = RevitImageExporter.ExportView("Level 1");
///
/// // Export by ElementId:
/// var result = RevitImageExporter.ExportView(viewId);
///
/// // Export with custom options:
/// var result = RevitImageExporter.ExportActiveView(new ImageExportSettings { PixelSize = 2048 });
///
/// // Save to file instead of base64:
/// var filePath = RevitImageExporter.ExportActiveViewToFile(@"C:\temp");
///
/// // Check before export:
/// if (RevitImageExporter.CanExport(view))
///     var result = RevitImageExporter.ExportView(view);
/// </code>
/// </example>
[PublicAPI]
public static class RevitImageExporter
{
    private static readonly HashSet<ViewType> NonExportableViewTypes =
    [
        ViewType.Internal,
        ViewType.ProjectBrowser,
        ViewType.SystemBrowser,
        ViewType.Undefined,
    ];

    /// <summary>
    /// Check whether a view can be exported as an image.
    /// </summary>
    public static bool CanExport(View view)
    {
        if (view.IsTemplate)
            return false;

        return !NonExportableViewTypes.Contains(view.ViewType);
    }

    /// <summary>
    /// Export the currently active view as a base64-encoded image.
    /// </summary>
    public static ImageExportResult ExportActiveView(ImageExportSettings? settings = null)
    {
        var view = RevitContext.ActiveView ?? throw new InvalidOperationException("No active view.");
        return ExportView(view, settings);
    }

    /// <summary>
    /// Export the active graphical view (excludes schedules/sheets) as a base64-encoded image.
    /// </summary>
    public static ImageExportResult ExportActiveGraphicalView(ImageExportSettings? settings = null)
    {
        var view = RevitContext.ActiveGraphicalView ?? throw new InvalidOperationException("No active graphical view.");
        return ExportView(view, settings);
    }

    /// <summary>
    /// Export a view by name as a base64-encoded image.
    /// </summary>
    public static ImageExportResult ExportView(string viewName, ImageExportSettings? settings = null)
    {
        var doc = RevitContext.ActiveDocument ?? throw new InvalidOperationException("No active document.");
        var view = FindViewByName(doc, viewName) ?? throw new ArgumentException($"View '{viewName}' not found.");
        return ExportView(view, settings);
    }

    /// <summary>
    /// Export a view by ElementId as a base64-encoded image.
    /// </summary>
    public static ImageExportResult ExportView(ElementId viewId, ImageExportSettings? settings = null)
    {
        var doc = RevitContext.ActiveDocument ?? throw new InvalidOperationException("No active document.");
        var view = doc.GetElement(viewId) as View ?? throw new ArgumentException($"Element '{viewId}' is not a view.");
        return ExportView(view, settings);
    }

    /// <summary>
    /// Export a view as a base64-encoded image.
    /// Uses <see cref="Document.ExportImage"/> for graphical views; schedules use sheet-based export.
    /// </summary>
    public static ImageExportResult ExportView(View view, ImageExportSettings? settings = null)
    {
        EnsureExportable(view);

        settings ??= new ImageExportSettings();

        var exportDir = GetExportDirectory();
        var filePath = ExportViewToFile(view, exportDir, settings);

        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            throw new InvalidOperationException($"Failed to export view '{view.Name}' to image file.");

        try
        {
            var format = GetExportFormat(settings.FileType);
            var imageBytes = File.ReadAllBytes(filePath);
            return new ImageExportResult(
                Convert.ToBase64String(imageBytes),
                ContentType: format.ContentType,
                ViewName: view.Name,
                ViewId: view.Id,
                FileSizeBytes: imageBytes.Length,
                PixelSize: settings.PixelSize);
        }
        finally
        {
            TryDeleteFile(filePath);
        }
    }

    /// <summary>
    /// Export multiple views as base64-encoded images. Non-exportable views are silently skipped.
    /// </summary>
    public static IReadOnlyList<ImageExportResult> ExportViews(IEnumerable<ElementId> viewIds, ImageExportSettings? settings = null)
    {
        var doc = RevitContext.ActiveDocument ?? throw new InvalidOperationException("No active document.");

        var results = new List<ImageExportResult>();
        foreach (var viewId in viewIds)
        {
            if (doc.GetElement(viewId) is not View view || !CanExport(view)) continue;
            results.Add(ExportView(view, settings));
        }
        return results;
    }

    /// <summary>
    /// Export the active view to <paramref name="outputDirectory"/>.
    /// The output file is named <c>{ViewName}.{ext}</c> where the extension is derived from <see cref="ImageExportSettings.FileType"/>.
    /// Returns the full path of the created file.
    /// </summary>
    public static string ExportActiveViewToFile(string outputDirectory, ImageExportSettings? settings = null)
    {
        var view = RevitContext.ActiveView ?? throw new InvalidOperationException("No active view.");
        return ExportViewToFile(view, outputDirectory, settings);
    }

    /// <summary>
    /// Export a view into <paramref name="outputDirectory"/>.
    /// The output file is named <c>{ViewName}.{ext}</c> where the extension is derived from <see cref="ImageExportSettings.FileType"/>.
    /// Returns the full path of the created file.
    /// </summary>
    public static string ExportViewToFile(View view, string outputDirectory, ImageExportSettings? settings = null)
    {
        EnsureExportable(view);

        settings ??= new ImageExportSettings();
        Directory.CreateDirectory(outputDirectory);

        var exported = view is ViewSchedule schedule
            ? ExportViaSheet(schedule, outputDirectory, settings)
            : ExportViaApi(view, outputDirectory, settings);

        var targetPath = Path.Combine(outputDirectory, $"{BuildFileName(view)}{GetExportFormat(settings.FileType).Extension}");

        File.Move(exported, targetPath, overwrite: true);

        // For schedule exports, clamp aspect ratio if needed to avoid excessively tall images that may cause issues for AI agents.
        if (view is ViewSchedule && settings.MaxAspectRatio.HasValue)
            ClampAspectRatio(targetPath, settings.MaxAspectRatio.Value, settings.PixelSize, settings.FileType);

        return targetPath;
    }

    private static (string ContentType, string Extension) GetExportFormat(ImageFileType fileType) => fileType switch
    {
        ImageFileType.PNG => ("image/png", ".png"),
        ImageFileType.BMP => ("image/bmp", ".bmp"),
        ImageFileType.TARGA => ("image/tga", ".tga"),
        ImageFileType.TIFF => ("image/tiff", ".tif"),
        _ => ("image/jpeg", ".jpg"),
    };

    private static string BuildFileName(View view)
    {
        var viewName = view.Name;
        var docTitle = view.Document.Title;
        var invalidChars = Path.GetInvalidFileNameChars();
        var stringBuilder = new StringBuilder(viewName.Length + docTitle.Length + 16);
        stringBuilder.Append(view.Id);
        stringBuilder.Append('_');
        foreach (var c in viewName)
        {
            stringBuilder.Append(Array.IndexOf(invalidChars, c) >= 0 ? '_' : c);
        }
        stringBuilder.Append('_');
        foreach (var c in docTitle)
        {
            stringBuilder.Append(Array.IndexOf(invalidChars, c) >= 0 ? '_' : c);
        }
        return stringBuilder.ToString();
    }

    /// <summary>
    /// Scales the image to <paramref name="pixelSize"/> width (upscaling only), then crops the height
    /// to <c>pixelSize * maxRatio</c>, keeping the top portion. No-op when already within ratio.
    /// </summary>
    private static void ClampAspectRatio(string filePath, double maxRatio, int pixelSize, ImageFileType fileType)
    {
        var encoder = GetImageEncoder(fileType);
        if (encoder is null) return;

        var bytes = File.ReadAllBytes(filePath);
        using var ms = new MemoryStream(bytes);
        using var src = Image.FromStream(ms);

        var scale = (double) pixelSize / src.Width;
        var scaledH = (int) (src.Height * scale);
        var finalH = (int) Math.Min(scaledH, pixelSize * maxRatio);

        if (src.Width == pixelSize && src.Height <= finalH) return;

        using var dst = new Bitmap(pixelSize, finalH);
        using var g = Graphics.FromImage(dst);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.DrawImage(src, 0, 0, pixelSize, scaledH); // bitmap bounds clip excess height

        dst.Save(filePath, encoder, null);
    }

    private static ImageCodecInfo? GetImageEncoder(ImageFileType fileType)
    {
        var mime = GetExportFormat(fileType).ContentType;
        foreach (var encoder in ImageCodecInfo.GetImageEncoders())
        {
            if (encoder.MimeType == null) continue;
            if (encoder.MimeType.Equals(mime, StringComparison.OrdinalIgnoreCase))
                return encoder;
        }
        return null;
    }

    private static void EnsureExportable(View view)
    {
        if (!CanExport(view))
            throw new ArgumentException($"View '{view.Name}' (type={view.ViewType}) is not exportable.");
    }

    private static string ExportViaApi(View view, string outputDir, ImageExportSettings settings)
    {
        var doc = view.Document;
        var exportBasePath = GetExportBasePath(outputDir, view);

        var options = CreateExportOptions(exportBasePath, settings, view.Id);

        doc.ExportImage(options);

        return ResolveExportedFile(outputDir, exportBasePath, view.Name);
    }

    /// <summary>
    /// Sheet-based export for schedules: creates a temporary sheet, places the schedule on it,
    /// exports the sheet as an image, then rolls back all document changes.
    /// </summary>
    private static string ExportViaSheet(ViewSchedule view, string outputDir, ImageExportSettings settings)
    {
        if (view.IsTitleblockRevisionSchedule)
            throw new NotSupportedException("Revision schedules are not supported for export due to API limitations.");
        
        var doc = view.Document;
        var exportBasePath = GetExportBasePath(outputDir, view);

        ExternalEventController.ActionEventHandler.Raise(() => 
        {
            using var tg = new TransactionGroup(doc, "Schedule Export");
            tg.Start();

            using var tr = new Transaction(doc, "Setup Schedule Export");
            tr.Start();
            var sheet = ViewSheet.Create(doc, ElementId.InvalidElementId);
            ScheduleSheetInstance.Create(doc, sheet.Id, view.Id, new XYZ(1, 1, 0));
            tr.Commit();

            var options = CreateExportOptions(exportBasePath, settings, sheet.Id);
            try
            {
                doc.ExportImage(options);
            }
            finally
            {
                tg.RollBack();
            }
        });

        return ResolveExportedFile(outputDir, exportBasePath, view.Name);
    }

    private static ImageExportOptions CreateExportOptions(string exportBasePath, ImageExportSettings settings, ElementId targetViewId)
    {
        var options = new ImageExportOptions
        {
            ExportRange = ExportRange.SetOfViews,
            FilePath = exportBasePath,
            HLRandWFViewsFileType = settings.FileType,
            ShadowViewsFileType = settings.FileType,
            ImageResolution = settings.Resolution,
            ZoomType = ZoomFitType.FitToPage,
            PixelSize = settings.PixelSize,
        };

        options.SetViewsAndSheets([targetViewId]);
        return options;
    }

    private static string ResolveExportedFile(string outputDir, string exportBasePath, string viewName)
    {
        var exported = FindExportedFile(outputDir, Path.GetFileName(exportBasePath));
        return exported ?? throw new InvalidOperationException($"Image export failed for view '{viewName}'.");
    }

    private static string GetExportBasePath(string outputDir, View view)
        => Path.Combine(outputDir, $"rdt_export_{view.Id}");

    private static View? FindViewByName(Document doc, string viewName)
    {
        using var collector = new FilteredElementCollector(doc);
        var views = collector.OfClass(typeof(View)).ToElements();
        foreach (var element in views)
        {
            if (element is View view && CanExport(view) && string.Equals(view.Name, viewName, StringComparison.OrdinalIgnoreCase))
            {
                return view;
            }
        }
        return null;
    }

    private static string? FindExportedFile(string directory, string prefix)
    {
        var candidates = Directory.GetFiles(directory, $"{prefix}*.*");
        return candidates.Length == 0 ? null : candidates.OrderByDescending(File.GetCreationTimeUtc).First();
    }

    private static string GetExportDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "RevitDevTool", "ImageExports");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void TryDeleteFile(string path)
    {
        try { File.Delete(path); }
        catch { /* best-effort cleanup */ }
    }
}

/// <summary>
/// Configuration for image export operations.
/// </summary>
[PublicAPI]
public sealed class ImageExportSettings
{
    /// <summary>Image width in pixels. Default: 1024.</summary>
    public int PixelSize { get; set; } = 1024;

    /// <summary>Image DPI. Default: DPI_150.</summary>
    public ImageResolution Resolution { get; set; } = ImageResolution.DPI_150;

    /// <summary>Output format. Default: PNG.</summary>
    public ImageFileType FileType { get; set; } = ImageFileType.PNG;

    /// <summary>
    /// Maximum height-to-width ratio for schedule exports. When exceeded, the image is scaled to
    /// <see cref="PixelSize"/> width and the height is clamped, keeping the top portion only.
    /// Default: 2.0. Set to <c>null</c> to disable schedule clamping.
    /// </summary>
    public double? MaxAspectRatio { get; set; } = 2.0;
}

/// <summary>
/// Result of an image export operation.
/// </summary>
[PublicAPI]
public sealed record ImageExportResult(
    string Base64Data,
    string ContentType,
    string ViewName,
    ElementId ViewId,
    int FileSizeBytes,
    int PixelSize);
