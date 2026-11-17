namespace RevitDevTool.ModelsContext.LiveTests;

internal static class ViewImage
{
    private const int PixelSize = 1600;

    public static void Export(Document document, View view, string path)
    {
        var folder = Path.Combine(Path.GetTempPath(), "mc-view-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var options = new ImageExportOptions
            {
                ExportRange = ExportRange.SetOfViews,
                FilePath = Path.Combine(folder, "view"),
                HLRandWFViewsFileType = ImageFileType.PNG,
                ShadowViewsFileType = ImageFileType.PNG,
                ZoomType = ZoomFitType.FitToPage,
                PixelSize = PixelSize,
                ImageResolution = ImageResolution.DPI_150
            };
            options.SetViewsAndSheets([view.Id]);
            document.ExportImage(options);
            var image = Directory.GetFiles(folder, "*.png").FirstOrDefault()
                        ?? throw new InvalidOperationException($"Revit wrote no image for view '{view.Name}'.");
            File.Copy(image, path, true);
        }
        finally
        {
            try
            {
                Directory.Delete(folder, true);
            }
            catch (IOException)
            {
                // ignore
            }
        }
    }
}
