using System.IO;
using System.Text.Json;
using DevTools.Execution.Providers.Python;
using DevTools.Mcp.Core.Invocation;
using DevTools.Mcp.Core.Models;
using DevTools.Mcp.Core.Protocol;
using DevTools.Mcp.Core.Results;
using ModelContextProtocol.Protocol;
using Python.Runtime;

namespace DevTools.Execution.External.Mcp.Backends;

/// <summary>
/// Owns the complete Python MCP boundary: runtime invocation plus request/result JSON.
/// Protocol encoding is deliberately private to this backend.
/// </summary>
public sealed class PythonMcpToolBackend(PythonExecutor executor) : IMcpPrimitiveBackend
{
    public ExecutionMode SourceKind => ExecutionMode.Python;

    public async Task<McpResult<McpInvocationResponse>> InvokeToolAsync(
        McpRegisteredTool tool,
        CallToolRequestParams request,
        IHostContextExecutor hostContext,
        CancellationToken cancellationToken)
    {
        return await hostContext.ExecuteAsync(
            () => Invoke(tool, request),
            cancellationToken).ConfigureAwait(false);
    }

    private McpResult<McpInvocationResponse> Invoke(McpRegisteredTool tool, CallToolRequestParams request)
    {
        var sourcePath = RequireSourcePath(tool.Binding.SourcePath);
        var resultJson = executor.Execute(
            sourcePath,
            Path.GetDirectoryName(sourcePath) ?? string.Empty,
            scope =>
            {
                scope.Set(PythonInstances.ToolName, new PyString(tool.Descriptor.Name));
                scope.Set(PythonInstances.PayloadJson, new PyString(WriteRequest(request)));
                scope.Exec(PythonEmbedded.ToolInvokeScript);
                return scope.Get(PythonInstances.ResultJson).As<string>();
            });

        return McpResult<McpInvocationResponse>.Success(
            ToolsetResultSerializer.ToInvocationResponse(ReadToolResult(resultJson), tool.Descriptor.OutputSchema));
    }

    public ReadResourceResult ReadResource(
        McpRegisteredResource resource,
        string uri,
        CancellationToken cancellationToken)
    {
        var sourcePath = RequireSourcePath(resource.Binding.SourcePath);
        var resultJson = executor.Execute(
            sourcePath,
            Path.GetDirectoryName(sourcePath) ?? string.Empty,
            scope =>
            {
                scope.Set(PythonInstances.Operation, new PyString(PythonInstances.OperationResource));
                scope.Set(PythonInstances.ResourceName, new PyString(resource.DisplayName));
                scope.Set(PythonInstances.ResourceUri, new PyString(uri));
                scope.Exec(PythonEmbedded.ToolInvokeScript);
                return scope.Get(PythonInstances.ResultJson).As<string>();
            });
        return ReadResourceResult(resultJson);
    }

    public void ClearCaches()
    {
    }

    internal static string WriteRequest(CallToolRequestParams? request)
    {
        if (request is null)
            return "{}";

        if (request.InputResponses is not { Count: > 0 } && string.IsNullOrEmpty(request.RequestState))
            return request.Arguments is { Count: > 0 }
                ? JsonSerializer.Serialize(request.Arguments, ToolHelpers.ProtocolOptions)
                : "{}";

        var payload = new Dictionary<string, object?>();
        if (request.Arguments is { Count: > 0 } arguments)
            payload[McpSpecKeys.Tools.Arguments] = arguments;
        if (request.InputResponses is { Count: > 0 } inputResponses)
            payload[McpSpecKeys.Tools.InputResponses] = inputResponses;
        if (!string.IsNullOrEmpty(request.RequestState))
            payload[McpSpecKeys.Tools.RequestState] = request.RequestState;
        return JsonSerializer.Serialize(payload, ToolHelpers.ProtocolOptions);
    }

    internal static CallToolResult ReadToolResult(string json)
    {
        using var document = JsonDocument.Parse(json);
        ThrowIfInputRequired(document.RootElement);
        return Deserialize<CallToolResult>(document.RootElement, "tool");
    }

    internal static ReadResourceResult ReadResourceResult(string json)
    {
        using var document = JsonDocument.Parse(json);
        ThrowIfInputRequired(document.RootElement);
        var result = Deserialize<ReadResourceResult>(document.RootElement, "resource");
        if (result.Contents.Any(static item => item is not TextResourceContents and not BlobResourceContents))
            throw new InvalidOperationException("Python MCP resource contents must be text or blob entries.");
        return result;
    }

    private static T Deserialize<T>(JsonElement root, string kind)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(root.GetRawText(), ToolHelpers.ProtocolOptions)
                   ?? throw new InvalidOperationException($"Python MCP {kind} result was null.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Python MCP {kind} result was malformed.", ex);
        }
    }

    private static void ThrowIfInputRequired(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty(McpSpecKeys.ResultType.Key, out var resultType) ||
            resultType.GetString() != McpSpecKeys.ResultType.InputRequired)
            return;

        throw new InputRequiredException(Deserialize<InputRequiredResult>(root, "input-required"));
    }

    private static string RequireSourcePath(string? sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            throw new InvalidOperationException($"Python MCP source file was not found: {sourcePath}.");
        return sourcePath!;
    }
}
