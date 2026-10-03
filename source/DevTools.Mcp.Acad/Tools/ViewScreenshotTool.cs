using System.ComponentModel;
using System.Drawing.Imaging;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using DevTools.Mcp;
using DevTools.Mcp.Core.Protocol;
using DevTools.Mcp.Core.Utils;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace DevTools.Mcp.Acad.Tools;

/// <summary>
/// Captures the active AutoCAD viewport as PNG <see cref="ImageContentBlock"/> for vision models.
/// </summary>
public sealed class ViewScreenshotTool : IBuiltInMcpTool
{
    private const uint CaptureWidth = 1280;
    private const uint CaptureHeight = 720;

    public ViewScreenshotTool()
    {
        ServerTool = McpServerTool.Create(
            Capture,
            new McpServerToolCreateOptions
            {
                Name = McpSpecKeys.Tool.ViewScreenshot,
                Title = "View Screenshot",
                Description =
                    "Capture the current viewport as PNG (1280x720) image content. " +
                    $"Captures exactly what the user sees. Zoom via {McpSpecKeys.Tool.ExecutePython} first if needed.",
                ReadOnly = true,
                Destructive = false,
                OpenWorld = false
            });
    }

    public string Name => McpSpecKeys.Tool.ViewScreenshot;
    public McpServerTool ServerTool { get; }

    [Description("Capture the current viewport as PNG image content.")]
    private CallToolResult Capture()
    {
        var doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc is null)
            return ToolHelpers.ErrorResult("No active document. Open a drawing first.");

        try
        {
            using var bitmap = doc.CapturePreviewImage(CaptureWidth, CaptureHeight);
            if (bitmap is null)
                return ToolHelpers.ErrorResult("CapturePreviewImage returned null. Document window may be minimized.");

            using var ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Png);
            return ToolHelpers.ImageResult(ms.ToArray(), "image/png");
        }
        catch (Exception ex)
        {
            return ToolHelpers.ErrorResult($"Failed to capture: {ex.Message}");
        }
    }
}
