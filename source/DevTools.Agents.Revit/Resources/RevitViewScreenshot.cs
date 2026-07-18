using DevTools.Mcp.BuiltIn;
using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RevitDevTool.Core;

namespace DevTools.Agents.Revit.Resources;

/// <summary>
/// Captures a screenshot of the active Revit view as a base64-encoded PNG.
/// AI vision models can use this to verify visual results of operations.
/// </summary>
public sealed class RevitViewScreenshot : IBuiltInMcpResource
{
    public McpServerResource Primitive => McpServerResource.Create(typeof(RevitViewScreenshot).GetMethod(nameof(ReadViewScreenshot))!, this);

    [McpServerResource(UriTemplate = "revit://view/screenshot", Name = "revit_view_screenshot")]
    [Description("PNG screenshot of the active Revit view.")]
    public ReadResourceResult ReadViewScreenshot()
    {
        var view = RevitContext.ActiveView;
        if (view is null)
            return ErrorResult("revit://view/screenshot", "No active view available.");

        if (!RevitImageExporter.CanExport(view))
            return ErrorResult("revit://view/screenshot", $"View '{view.Name}' (type={view.ViewType}) cannot be exported as image.");

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
                BlobResourceContents.FromBytes(imageBytes, "revit://view/screenshot", "image/png")
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
