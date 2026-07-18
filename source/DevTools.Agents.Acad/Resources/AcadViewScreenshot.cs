using System.Drawing.Imaging;
using System.ComponentModel;
using Autodesk.AutoCAD.ApplicationServices;
using DevTools.Mcp.BuiltIn;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace DevTools.Agents.Acad.Resources;

/// <summary>
/// Captures the active AutoCAD viewport as a PNG image (1920x1080).
/// Respects the current viewport — does not manipulate zoom or view.
/// Agent should zoom via execute_python_code before calling if a different view is needed.
/// </summary>
public sealed class AcadViewScreenshot : IBuiltInMcpResource
{
    private const uint CaptureWidth = 1920;
    private const uint CaptureHeight = 1080;

    public McpServerResource Primitive => McpServerResource.Create(typeof(AcadViewScreenshot).GetMethod(nameof(ReadViewScreenshot))!, this);

    [McpServerResource(UriTemplate = "acad://view/screenshot", Name = "acad_view_screenshot")]
    [Description("PNG screenshot of the current AutoCAD viewport.")]
    public ReadResourceResult ReadViewScreenshot()
    {
        var doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc is null)
            return ErrorResult("acad://view/screenshot", "No active document. Open a drawing first.");

        try
        {
            using var bitmap = doc.CapturePreviewImage(CaptureWidth, CaptureHeight);
            if (bitmap is null)
                return ErrorResult("acad://view/screenshot", "CapturePreviewImage returned null. Document window may be minimized.");

            using var ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Png);
            var imageBytes = ms.ToArray();

            return new ReadResourceResult
            {
                Contents =
                [
                BlobResourceContents.FromBytes(imageBytes, "acad://view/screenshot", "image/png")
                ]
            };
        }
        catch (Exception ex)
        {
            return ErrorResult("acad://view/screenshot", $"Failed to capture: {ex.Message}");
        }
    }

    private static ReadResourceResult ErrorResult(string uri, string message) =>
        new()
        {
            Contents =
            [
                new TextResourceContents
                {
                    Uri = uri,
                    MimeType = "text/plain",
                    Text = message
                }
            ]
        };
}
