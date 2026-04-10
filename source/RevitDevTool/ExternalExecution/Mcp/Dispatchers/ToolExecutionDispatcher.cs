using System.Collections.Concurrent;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Python.Runtime;
using RevitDevTool.Execution.Providers.Python;
using RevitDevTool.ExternalExecution.Mcp.Execution;
using RevitDevTool.McpParser;
using RevitDevTool.McpParser.Dotnet;
using RevitDevTool.McpParser.Models;
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace RevitDevTool.ExternalExecution.Mcp.Dispatchers;

/// <summary>
/// Dispatches MCP tool calls to the appropriate execution backend.
/// Dotnet tools are invoked asynchronously; Python tools run synchronously under the GIL.
/// </summary>
public sealed class ToolExecutionDispatcher(
    IServiceProvider serviceProvider,
    PythonInitializer pythonInitializer) : ICacheable
{
    private readonly ConcurrentDictionary<string, McpServerTool> _cachedTools = new(StringComparer.OrdinalIgnoreCase);

    public async Task<McpToolExecutionResult> DispatchAsync(McpRegisteredTool tool, string? payloadJson)
    {
        try
        {
            var normalizedPayload = NormalizePayload(payloadJson);

            return tool.Binding.SourceKind switch
            {
                ExecutionMode.Assembly => await InvokeDotnetToolAsync(tool, normalizedPayload).ConfigureAwait(false),
                ExecutionMode.Python => InvokePythonTool(pythonInitializer, tool, normalizedPayload),
                _ => McpToolExecutionResult.Failed(ExecutionErrorCodes.ToolUnknownSourceKind, $"Unknown or unsupported MCP tool execution: '{tool.Binding.SourceKind}'.")
            };
        }
        catch (Exception ex)
        {
            return McpToolExecutionResult.Failed(ExecutionErrorCodes.ToolInvokeFailed, ex.Message, ex.StackTrace);
        }
    }

    private async Task<McpToolExecutionResult> InvokeDotnetToolAsync(McpRegisteredTool tool, string normalizedPayload)
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
        var result = await serverTool.InvokeAsync(requestContext, CancellationToken.None).ConfigureAwait(false);
        return McpToolExecutionResult.Completed(result, $"Completed '{tool.ProtocolTool.Name}'.");
    }

    public void ClearCache() => _cachedTools.Clear();

    private McpServerTool? GetOrCreateServerTool(McpRegisteredTool tool)
    {
        return DotnetMcpServerFactory.GetOrCreate(
            _cachedTools,
            tool.Id,
            tool,
            DotnetMethodResolver.ResolveTool,
            serviceProvider,
            (method, target) => McpServerTool.Create(method, target));
    }

    private static McpToolExecutionResult InvokePythonTool(PythonInitializer initializer, McpRegisteredTool tool, string normalizedPayload)
    {
        var binding = tool.Binding;
        var resultJson = PythonExecutionHelper.InvokeScript(initializer, binding.SourcePath, scope =>
        {
            scope.Set(PythonScopeVars.ToolName, new PyString(tool.ProtocolTool.Name));
            scope.Set(PythonScopeVars.PayloadJson, new PyString(normalizedPayload));
        });
        var callResult = PythonResultParser.ParseCallToolResult(resultJson);
        return McpToolExecutionResult.Completed(callResult, $"Completed '{tool.ProtocolTool.Name}'.");
    }

    private static string NormalizePayload(string? payloadJson)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson!);
        return doc.RootElement.ValueKind != JsonValueKind.Object
            ? throw new JsonException("Tool payload must be a JSON object.")
            : doc.RootElement.GetRawText();
    }
}
