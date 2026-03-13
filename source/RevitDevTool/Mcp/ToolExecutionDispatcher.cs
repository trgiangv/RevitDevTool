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
using RevitDevTool.Mcp.Parser;
using RevitDevTool.Mcp.Parser.Dotnet;
using RevitDevTool.Mcp.Parser.Models;

namespace RevitDevTool.Mcp;

/// <summary>
/// Dispatches MCP tool calls. All methods are synchronous because they run
/// inside Revit's <c>IExternalEventHandler.Execute()</c> on the main thread.
/// </summary>
public sealed class ToolExecutionDispatcher(
    IServiceProvider serviceProvider)
{
    private static readonly JsonSerializerOptions JsonOptions = McpJsonUtilities.DefaultOptions;
    private readonly ConcurrentDictionary<string, McpServerTool> _cachedTools = new(StringComparer.OrdinalIgnoreCase);

    public McpToolExecutionResult Dispatch(McpRegisteredTool tool, string? payloadJson)
    {
        try
        {
            var normalizedPayload = NormalizePayload(payloadJson);

            return tool.Binding.SourceKind switch
            {
                ExecutionMode.Assembly => InvokeDotnetTool(tool, normalizedPayload),
                ExecutionMode.Python => InvokePythonTool(tool, normalizedPayload),
                _ => McpToolExecutionResult.Failed(ExecutionErrorCodes.ToolUnknownSourceKind, $"Unknown or unsupported MCP tool execution: '{tool.Binding.SourceKind}'.")
            };
        }
        catch (Exception ex)
        {
            return McpToolExecutionResult.Failed(ExecutionErrorCodes.ToolInvokeFailed, ex.Message, ex.StackTrace);
        }
    }

    private McpToolExecutionResult InvokeDotnetTool(McpRegisteredTool tool, string normalizedPayload)
    {
        var serverTool = GetOrCreateServerTool(tool);
        if (serverTool is null)
            return McpToolExecutionResult.Failed(ExecutionErrorCodes.ToolNotImplemented, $"No .NET tool method mapped for '{tool.ProtocolTool.Name}'.");

        using var doc = JsonDocument.Parse(normalizedPayload);
        var arguments = new Dictionary<string, JsonElement>();
        foreach (var prop in doc.RootElement.EnumerateObject())
            arguments[prop.Name] = prop.Value;

        var requestParams = new CallToolRequestParams { Name = tool.ProtocolTool.Name, Arguments = arguments };
        var requestContext = RequestContextFactory.Create(requestParams, RequestMethods.ToolsCall);
        var result = serverTool.InvokeAsync(requestContext, CancellationToken.None)
            .ConfigureAwait(false).GetAwaiter().GetResult();

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

    private static McpToolExecutionResult InvokePythonTool(McpRegisteredTool tool, string normalizedPayload)
    {
        var binding = tool.Binding;

        PythonInitializer.InitializeAsync().GetAwaiter().GetResult();

        if (string.IsNullOrWhiteSpace(binding.SourcePath) || !File.Exists(binding.SourcePath))
            return McpToolExecutionResult.Failed(ExecutionErrorCodes.ToolSourceNotFound, $"Python MCP source file was not found for '{tool.ProtocolTool.Name}'.", $"sourcePath={binding.SourcePath}");

        using (Py.GIL())
        {
            if (PythonInitializer.GlobalScope is null)
                return McpToolExecutionResult.Failed(ExecutionErrorCodes.ToolPythonRuntimeUnavailable, "Global Python scope not initialized.");

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
        using var doc = JsonDocument.Parse(resultJson);
        var root = doc.RootElement;

        return root.ValueKind switch
        {
            JsonValueKind.Null => new CallToolResult(),
            JsonValueKind.String => new CallToolResult { Content = [new TextContentBlock { Text = root.GetString() ?? string.Empty }] },
            JsonValueKind.Array => new CallToolResult { Content = ParseContentBlocks(root) },
            JsonValueKind.Object when root.TryGetProperty("content", out var contentProp) =>
                new CallToolResult { Content = ParseContentBlocks(contentProp) },
            _ => new CallToolResult { Content = [new TextContentBlock { Text = resultJson }] },
        };
    }

    private static IList<ContentBlock> ParseContentBlocks(JsonElement array)
    {
        var blocks = new List<ContentBlock>();
        foreach (var element in array.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.Object
                && element.TryGetProperty("type", out var typeProp)
                && typeProp.GetString() is "text"
                && element.TryGetProperty("text", out var textProp))
            {
                blocks.Add(new TextContentBlock { Text = textProp.GetString() ?? string.Empty });
            }
            else
            {
                blocks.Add(new TextContentBlock { Text = element.GetRawText() });
            }
        }

        return blocks.Count > 0 ? blocks : [new TextContentBlock { Text = array.GetRawText() }];
    }

    private static string NormalizePayload(string? payloadJson)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson!);
        return doc.RootElement.ValueKind != JsonValueKind.Object
            ? throw new JsonException("Tool payload must be a JSON object.")
            : doc.RootElement.GetRawText();
    }
}
