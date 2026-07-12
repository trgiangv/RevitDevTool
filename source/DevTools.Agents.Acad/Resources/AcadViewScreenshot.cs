using System.Drawing.Imaging;
using Autodesk.AutoCAD.ApplicationServices;
using DevTools.Mcp.BuiltIn;
using ModelContextProtocol.Protocol;
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

    public string UriTemplate => "acad://view/screenshot";

    public Resource ProtocolResource { get; } = new()
    {
        Uri = "acad://view/screenshot",
        Name = "AutoCAD View Screenshot",
        Description = "Screenshot of the current viewport as PNG (1920x1080). Captures exactly what the user sees. Use ZOOM Extents via execute_python_code first if you need a full-drawing overview.",
        MimeType = "image/png"
    };

    public ReadResourceResult Read(string uri)
    {
        var doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc is null)
            return ErrorResult(uri, "No active document. Open a drawing first.");

        try
        {
            using var bitmap = doc.CapturePreviewImage(CaptureWidth, CaptureHeight);
            if (bitmap is null)
                return ErrorResult(uri, "CapturePreviewImage returned null. Document window may be minimized.");

            using var ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Png);
            var imageBytes = ms.ToArray();

            return new ReadResourceResult
            {
                Contents =
                [
                    BlobResourceContents.FromBytes(imageBytes, uri, "image/png")
                ]
            };
        }
        catch (Exception ex)
        {
            return ErrorResult(uri, $"Failed to capture: {ex.Message}");
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
