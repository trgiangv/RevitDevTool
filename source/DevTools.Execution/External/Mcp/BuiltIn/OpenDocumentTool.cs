using System.IO;
using System.Text.Json;
using DevTools.Execution.Interfaces;
using DevTools.McpParser.Models;
using ModelContextProtocol.Protocol;

namespace DevTools.Execution.External.Mcp.BuiltIn;

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
            type = "object",
            properties = new
            {
                file_path = new
                {
                    type = "string",
                    description = "Full path to the document file."
                }
            },
            required = new[] { "file_path" }
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
        if (!doc.RootElement.TryGetProperty("file_path", out var pathElement) ||
            pathElement.ValueKind != JsonValueKind.String)
        {
            return McpToolExecutionResult.Failed(
                ExecutionErrorCodes.ToolInvokeFailed, "Missing required 'file_path' parameter.");
        }

        var filePath = pathElement.GetString();
        if (string.IsNullOrWhiteSpace(filePath))
            return McpToolExecutionResult.Failed(
                ExecutionErrorCodes.ToolInvokeFailed, "file_path must not be empty.");

        if (!File.Exists(filePath))
            return McpToolExecutionResult.Failed(
                ExecutionErrorCodes.ToolInvokeFailed, $"File not found: {filePath}");

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
