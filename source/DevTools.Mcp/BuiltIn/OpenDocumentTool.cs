using System.IO;
using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.BuiltIn;

/// <summary>Opens a document file in the running host process via <see cref="IDocumentBridge"/>.</summary>
public sealed class OpenDocumentTool(IDocumentBridge documentBridge) : IBuiltInMcpTool
{
    public string Name => "open_document";

    public Tool ProtocolTool { get; } = new()
    {
        Name = "open_document",
        Description =
            "Open a document file in the running host process.\n" +
            "Revit: opens .rvt/.rfa files via UIApplication.\n" +
            "AutoCAD: opens .dwg/.dxf/.dwt files via DocumentManager.",
        InputSchema = JsonSerializer.SerializeToElement(new
        {
            type = JsonSchemaTypeNames.Object,
            properties = new
            {
                filePath = new
                {
                    type = JsonSchemaTypeNames.String,
                    description = "Full path to the document file."
                }
            },
            required = new[] { McpPropertyNames.FilePath }
        }),
        Annotations = new ToolAnnotations
        {
            Title = "Open Document",
            DestructiveHint = true,
            OpenWorldHint = true
        }
    };

    public async Task<McpToolExecutionResult> ExecuteAsync(string payloadJson, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(payloadJson);
        if (!doc.RootElement.TryGetProperty(McpPropertyNames.FilePath, out var pathElement) ||
            pathElement.ValueKind != JsonValueKind.String)
        {
            return McpToolExecutionResult.Failed(
                McpExecutionErrorCodes.ToolInvokeFailed, $"Missing required '{McpPropertyNames.FilePath}' parameter.");
        }

        var filePath = pathElement.GetString();
        if (string.IsNullOrWhiteSpace(filePath))
            return McpToolExecutionResult.Failed(
                McpExecutionErrorCodes.ToolInvokeFailed, $"{McpPropertyNames.FilePath} must not be empty.");

        if (!File.Exists(filePath))
            return McpToolExecutionResult.Failed(
                McpExecutionErrorCodes.ToolInvokeFailed, $"File not found: {filePath}");

        var result = await documentBridge.OpenDocumentAsync(filePath!, ct).ConfigureAwait(false);

        var callResult = new CallToolResult
        {
            IsError = !result.Success,
            Content = [new TextContentBlock
            {
                Text = JsonSerializer.Serialize(new
                {
                    success = result.Success,
                    message = result.Message,
                    documentTitle = result.DocumentTitle
                })
            }]
        };

        return McpToolExecutionResult.Completed(callResult, result.Message);
    }
}
