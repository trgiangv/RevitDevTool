using System.ComponentModel;
using System.IO;
using DevTools.Execution.Abstractions;
using DevTools.Mcp.Catalog;
using DevTools.Mcp.Core.Utils;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RevitDevTool.Core;

namespace DevTools.Agents.Revit.Tools;

/// <summary>
/// Captures a screenshot of the active Revit view as PNG <see cref="ImageContentBlock"/> for vision models.
/// </summary>
public sealed class ViewScreenshotTool : IBuiltInMcpTool
{
    private readonly IHostContextExecutor _hostContext;

    public ViewScreenshotTool(IHostContextExecutor hostContext)
    {
        _hostContext = hostContext;
        ServerTool = McpServerTool.Create(
            CaptureAsync,
            new McpServerToolCreateOptions
            {
                Name = "view_screenshot",
                Title = "View Screenshot",
                Description =
                    "Capture the active Revit view as a PNG image. " +
                    "Returns native MCP image content for vision verification after modifications.",
                ReadOnly = true,
                Destructive = false,
                OpenWorld = false
            });
    }

    public string Name => "view_screenshot";
    public McpServerTool ServerTool { get; }

    [Description("Capture the active view as PNG image content.")]
    private async Task<CallToolResult> CaptureAsync(CancellationToken cancellationToken = default)
    {
        return await _hostContext.ExecuteAsync(Capture, cancellationToken).ConfigureAwait(false);
    }

    private static CallToolResult Capture()
    {
        var view = RevitContext.ActiveView;
        if (view is null)
            return ToolHelpers.ErrorResult("No active view available.");

        if (!RevitImageExporter.CanExport(view))
            return ToolHelpers.ErrorResult($"View '{view.Name}' (type={view.ViewType}) cannot be exported as image.");

        var settings = new ImageExportSettings
        {
            PixelSize = 1280,
            Resolution = ImageResolution.DPI_150,
            FileType = ImageFileType.PNG
        };

        var tempDir = Path.Combine(Path.GetTempPath(), "RevitDevTool", Guid.NewGuid().ToString("N"));
        try
        {
            var filePath = RevitImageExporter.ExportActiveViewToFile(tempDir, settings);
            var imageBytes = File.ReadAllBytes(filePath);
            return ToolHelpers.ImageResult(imageBytes, "image/png");
        }
        catch (Exception ex)
        {
            return ToolHelpers.ErrorResult($"Failed to capture view: {ex.Message}");
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
}
