using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Python.Runtime;
using RevitDevTool.Contracts;
using RevitDevTool.Execution.Providers.Python;
using RevitDevTool.Mcp.Models;
using RevitDevTool.Mcp.Parser;
using RevitDevTool.Mcp.Parser.Dotnet;
using RevitDevTool.Mcp.Parser.Models;

namespace RevitDevTool.Mcp;

public sealed class McpToolExecutionDispatcher(
    IServiceProvider serviceProvider)
{
    private static readonly JsonSerializerOptions JsonOptions = McpJsonUtilities.DefaultOptions;
    private readonly ConcurrentDictionary<string, McpServerTool> _cachedTools = new(StringComparer.OrdinalIgnoreCase);

    public async Task<McpToolExecutionResult> DispatchAsync(
        McpRegisteredTool tool,
        string? payloadJson,
        IProgress<McpProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedPayload = NormalizePayload(payloadJson);
            cancellationToken.ThrowIfCancellationRequested();

            return tool.Binding.SourceKind switch
            {
                ExecutionMode.Assembly => await InvokeDotnetToolAsync(tool, normalizedPayload, progress, cancellationToken).ConfigureAwait(false),
                ExecutionMode.Python => InvokePythonTool(tool, normalizedPayload, progress),
                _ => McpToolExecutionResult.Failed(BridgeErrorCodes.ToolUnknownSourceKind, $"Unknown or unsupported MCP tool execution: '{tool.Binding.SourceKind}'.")
            };
        }
        catch (OperationCanceledException)
        {
            return McpToolExecutionResult.Cancelled($"Tool '{tool.ProtocolTool.Name}' was cancelled.");
        }
        catch (Exception ex)
        {
            return McpToolExecutionResult.Failed(BridgeErrorCodes.ToolInvokeFailed, ex.Message, ex.StackTrace);
        }
    }

    private async Task<McpToolExecutionResult> InvokeDotnetToolAsync(
        McpRegisteredTool tool,
        string normalizedPayload,
        IProgress<McpProgressUpdate>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report(new McpProgressUpdate { State = ExecutionState.Preparing, Detail = $"Binding .NET MCP tool '{tool.ProtocolTool.Name}'..." });

        var serverTool = GetOrCreateServerTool(tool);
        if (serverTool is null)
            return McpToolExecutionResult.Failed(BridgeErrorCodes.ToolNotImplemented, $"No .NET tool method mapped for '{tool.ProtocolTool.Name}'.");

        progress?.Report(new McpProgressUpdate { State = ExecutionState.Running, Detail = $"Executing .NET MCP tool '{tool.ProtocolTool.Name}'..." });

        using var doc = JsonDocument.Parse(normalizedPayload);
        var arguments = new Dictionary<string, JsonElement>();
        foreach (var prop in doc.RootElement.EnumerateObject())
            arguments[prop.Name] = prop.Value;

        var requestParams = new CallToolRequestParams { Name = tool.ProtocolTool.Name, Arguments = arguments };
        var requestContext = RequestContextFactory.Create(requestParams, RequestMethods.ToolsCall);
        var result = await serverTool.InvokeAsync(requestContext, cancellationToken).ConfigureAwait(false);

        return McpToolExecutionResult.Completed(result, $"Completed '{tool.ProtocolTool.Name}'.");
    }

    private McpServerTool? GetOrCreateServerTool(McpRegisteredTool tool)
    {
        if (_cachedTools.TryGetValue(tool.Id, out var cached))
            return cached;

        var method = DotnetToolMethodResolver.Resolve(tool);
        if (method is null)
            return null;

        var target = method.IsStatic ? null : ActivatorUtilities.CreateInstance(serviceProvider, method.DeclaringType!);
        var serverTool = McpServerTool.Create(method, target);
        _cachedTools.TryAdd(tool.Id, serverTool);
        return serverTool;
    }

    private static McpToolExecutionResult InvokePythonTool(
        McpRegisteredTool tool,
        string normalizedPayload,
        IProgress<McpProgressUpdate>? progress)
    {
        var binding = tool.Binding;
        progress?.Report(new McpProgressUpdate { State = ExecutionState.Preparing, Detail = $"Preparing Python MCP tool '{tool.ProtocolTool.Name}'..." });

        PythonInitializer.InitializeAsync().GetAwaiter().GetResult();

        if (string.IsNullOrWhiteSpace(binding.SourcePath) || !File.Exists(binding.SourcePath))
            return McpToolExecutionResult.Failed(BridgeErrorCodes.ToolSourceNotFound, $"Python MCP source file was not found for '{tool.ProtocolTool.Name}'.", $"sourcePath={binding.SourcePath}");

        progress?.Report(new McpProgressUpdate { State = ExecutionState.Running, Detail = $"Executing Python MCP tool '{tool.ProtocolTool.Name}'..." });

        using (Py.GIL())
        {
            if (PythonInitializer.GlobalScope is null)
                return McpToolExecutionResult.Failed(BridgeErrorCodes.ToolPythonRuntimeUnavailable, "Global Python scope not initialized.");

            using var scope = PythonInitializer.GlobalScope.NewScope();
            PythonExecutor.PrepareExecutionScope(scope, binding.SourcePath);
            scope.Set("__tool_name__", new PyString(tool.ProtocolTool.Name));
            scope.Set("__payload_json__", new PyString(normalizedPayload));
            scope.Exec(PythonEmbedded.ToolInvokeScript);

            var resultJson = scope.Get("__result_json__").As<string>();
            var callResult = DeserializePythonResult(resultJson);
            return McpToolExecutionResult.Completed(callResult, $"Completed '{tool.ProtocolTool.Name}'.");
        }
    }

    private static CallToolResult DeserializePythonResult(string resultJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(resultJson);
            var root = doc.RootElement;

            switch (root.ValueKind)
            {
                case JsonValueKind.String:
                    return new CallToolResult { Content = [new TextContentBlock { Text = root.GetString() ?? string.Empty }] };
                case JsonValueKind.Null:
                    return new CallToolResult();
                case JsonValueKind.Array:
                {
                    var blocks = JsonSerializer.Deserialize<IList<ContentBlock>>(resultJson, JsonOptions);
                    if (blocks is not null) return new CallToolResult { Content = blocks };
                    break;
                }
                case JsonValueKind.Object when root.TryGetProperty("content", out _):
                {
                    var toolResult = JsonSerializer.Deserialize<CallToolResult>(resultJson, JsonOptions);
                    if (toolResult is not null) return toolResult;
                    break;
                }
            }

            return new CallToolResult { Content = [new TextContentBlock { Text = resultJson }] };
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"[MCP] Failed to deserialize Python result: {ex.Message}");
            return new CallToolResult { IsError = true, Content = [new TextContentBlock { Text = resultJson }] };
        }
    }

    private static string NormalizePayload(string? payloadJson)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson!);
        return doc.RootElement.ValueKind != JsonValueKind.Object
            ? throw new JsonException("Tool payload must be a JSON object.")
            : doc.RootElement.GetRawText();
    }
}
