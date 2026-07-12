using DevTools.Mcp.BuiltIn;
using ModelContextProtocol.Protocol;
using RevitDevTool.Core;

namespace DevTools.Agents.Revit.Resources;

/// <summary>
/// Captures a screenshot of the active Revit view as a base64-encoded PNG.
/// AI vision models can use this to verify visual results of operations.
/// </summary>
public sealed class RevitViewScreenshot : IBuiltInMcpResource
{
    public string UriTemplate => "revit://view/screenshot";

    public Resource ProtocolResource { get; } = new()
    {
        Uri = "revit://view/screenshot",
        Name = "Revit View Screenshot",
        Description = "Screenshot of the active view as PNG image. Use after modifications to visually verify results.",
        MimeType = "image/png"
    };

    public ReadResourceResult Read(string uri)
    {
        var view = RevitContext.ActiveView;
        if (view is null)
            return ErrorResult(uri, "No active view available.");

        if (!RevitImageExporter.CanExport(view))
            return ErrorResult(uri, $"View '{view.Name}' (type={view.ViewType}) cannot be exported as image.");

        var result = RevitImageExporter.ExportActiveView(new ImageExportSettings
        {
            PixelSize = 1920,
            FileType = ImageFileType.PNG
        });

        var imageBytes = Convert.FromBase64String(result.Base64Data);
        return new ReadResourceResult
        {
            Contents =
            [
                BlobResourceContents.FromBytes(imageBytes, uri, "image/png")
            ]
        };
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
