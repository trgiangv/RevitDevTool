using System.IO;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevTools.Mcp.BuiltIn;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Execution.External.Mcp.BuiltIn;

/// <summary>Opens a document file in the running host process via <see cref="IDocumentBridge"/>.</summary>
public sealed class OpenDocumentTool(IDocumentBridge documentBridge) : IBuiltInMcpTool
{
    public McpServerTool Primitive => McpServerTool.Create(typeof(OpenDocumentTool).GetMethod(nameof(OpenDocumentAsync))!, this);

    [McpServerTool(Name = "open_document")]
    [Description("Open a document file in the running CAD/BIM host.")]
    public async Task<CallToolResult> OpenDocumentAsync(
        [Description("Full path to the document file.")] string filePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new McpException("filePath must not be empty.");

        if (!File.Exists(filePath))
            throw new McpException($"File not found: {filePath}");

        var result = await documentBridge.OpenDocumentAsync(filePath, cancellationToken).ConfigureAwait(false);

        var callResult = new CallToolResult
        {
            IsError = !result.Success,
            Content = [new TextContentBlock
            {
                Text = JsonSerializer.Serialize(new OpenDocumentResult(
                    result.Success,
                    result.Message,
                    result.DocumentTitle))
            }]
        };

        return callResult;
    }
    
    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record OpenDocumentResult(
        [property: JsonPropertyName("success")] bool Success,
        [property: JsonPropertyName("message")] string? Message,
        [property: JsonPropertyName("documentTitle")] string? DocumentTitle);
}
