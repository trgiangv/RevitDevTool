using System.ComponentModel;
using System.IO;
using System.Text.Json.Serialization;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Execution.External.Mcp.BuiltIn;

/// <summary>Opens a document file in the running host process via <see cref="IDocumentBridge"/>.</summary>
public sealed class OpenDocumentTool : IBuiltInMcpTool
{
    private readonly IDocumentBridge _documentBridge;

    public OpenDocumentTool(IDocumentBridge documentBridge)
    {
        _documentBridge = documentBridge;
        ServerTool = McpServerTool.Create(
            OpenAsync,
            new McpServerToolCreateOptions
            {
                Name = "open_document",
                Title = "Open Document",
                Description =
                    "Open a document file in the running host process.\n" +
                    "Revit: opens .rvt/.rfa files via UIApplication.\n" +
                    "AutoCAD: opens .dwg/.dxf/.dwt files via DocumentManager.",
                Destructive = true,
                OpenWorld = true
            });
    }

    public string Name => "open_document";
    public McpServerTool ServerTool { get; }

    [Description("Open a document file in the running host process.")]
    private async Task<CallToolResult> OpenAsync(
        [Description("Full path to the document file.")] string filePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return ToolHelpers.ErrorResult("filePath must not be empty.");

        if (!File.Exists(filePath))
            return ToolHelpers.ErrorResult($"File not found: {filePath}");

        var result = await _documentBridge.OpenDocumentAsync(filePath, cancellationToken).ConfigureAwait(false);
        var payload = new OpenDocumentResult(
            result.Success,
            result.Message,
            result.DocumentTitle);

        return result.Success
            ? ToolHelpers.Result(payload)
            : ToolHelpers.ErrorResult(payload);
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record OpenDocumentResult(
        [property: JsonPropertyName(IpcPropertyNames.Success)] bool Success,
        [property: JsonPropertyName(IpcPropertyNames.Message)] string? Message,
        [property: JsonPropertyName("documentTitle")] string? DocumentTitle);
}
