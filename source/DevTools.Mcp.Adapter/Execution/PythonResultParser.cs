using System.Text.Json;
using DevTools.Mcp.Core.Protocol;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Adapter.Execution;

/// <summary>
/// Deserializes python-sdk JSON dumped by <c>ToolInvoke.py</c>
/// (<c>CallToolResult</c> / <c>ReadResourceResult</c> / <c>InputRequiredResult</c>).
/// </summary>
internal static class PythonResultParser
{
    public static ReadResourceResult ParseReadResourceResult(string resultJson)
    {
        using var document = JsonDocument.Parse(resultJson);
        var root = document.RootElement;
        ThrowIfInputRequired(root);

        try
        {
            var result = JsonSerializer.Deserialize<ReadResourceResult>(
                root.GetRawText(),
                McpJsonUtilities.DefaultOptions)
                ?? throw new InvalidOperationException("Python MCP resource result was null.");

            if (result.Contents.Any(static item => item is not TextResourceContents and not BlobResourceContents))
            {
                throw new InvalidOperationException(
                    "Python MCP resource contents must be SDK text or blob entries.");
            }

            return result;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Python MCP resource result was malformed.", ex);
        }
    }

    public static CallToolResult ParseCallToolResult(string resultJson)
    {
        using var document = JsonDocument.Parse(resultJson);
        var root = document.RootElement;
        ThrowIfInputRequired(root);

        try
        {
            return JsonSerializer.Deserialize<CallToolResult>(
                root.GetRawText(),
                McpJsonUtilities.DefaultOptions)
                ?? throw new InvalidOperationException("Python MCP result was null.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Python MCP tool result was malformed.", ex);
        }
    }

    private static void ThrowIfInputRequired(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty(McpSpecKeys.ResultType.Key, out var resultType) ||
            resultType.GetString() != McpSpecKeys.ResultType.InputRequired)
        {
            return;
        }

        try
        {
            var inputRequired = JsonSerializer.Deserialize<InputRequiredResult>(
                root.GetRawText(),
                McpJsonUtilities.DefaultOptions)
                ?? throw new InvalidOperationException("Python MCP input-required result was null.");
            throw new InputRequiredException(inputRequired);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                "Python MCP input-required result was malformed.",
                ex);
        }
    }
}
