using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using DevTools.McpParser;
using DevTools.Execution.External.Mcp.BuiltIn;
using DevTools.Execution.External.Mcp.Execution;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Providers.Python;
using DevTools.McpParser.Dotnet;
using DevTools.McpParser.Models;
using DevTools.Telemetry;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Python.Runtime;
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace DevTools.Execution.External.Mcp.Dispatchers;

/// <summary>Dispatches MCP tool calls to the appropriate execution backend.</summary>
public sealed class ToolExecutionDispatcher(
    IServiceProvider serviceProvider,
    PythonExecutor executor,
    DotnetMethodResolver methodResolver,
    IEnumerable<IBuiltInMcpTool> builtInTools,
    ITelemetry telemetry) : ICacheable
{
    private readonly ConcurrentDictionary<string, McpServerTool> _cachedTools = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IBuiltInMcpTool> _builtInIndex = builtInTools.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);

    public async Task<McpToolExecutionResult> DispatchAsync(
        McpRegisteredTool tool,
        string? payloadJson,
        IHostContextExecutor hostContext,
        CancellationToken ct = default)
    {
        telemetry.RecordMcpInvocation(tool.Binding.SourceKind.ToString());

        try
        {
            var normalizedPayload = NormalizePayload(payloadJson);

            return tool.Binding.SourceKind switch
            {
                ExecutionMode.Assembly => await hostContext
                    .ExecuteAsync(() => InvokeDotnetToolAsync(tool, normalizedPayload), ct)
                    .ConfigureAwait(false),
                ExecutionMode.Python => await hostContext
                    .ExecuteAsync(() => InvokePythonTool(executor, tool, normalizedPayload), ct)
                    .ConfigureAwait(false),
                ExecutionMode.CSharp => await InvokeCSharpToolAsync(tool.ProtocolTool.Name, normalizedPayload, ct)
                    .ConfigureAwait(false),
                _ => McpToolExecutionResult.Failed(ExecutionErrorCodes.ToolUnknownSourceKind, $"Unknown or unsupported MCP tool execution: '{tool.Binding.SourceKind}'.")
            };
        }
        catch (Exception ex)
        {
            if (TelemetryReporting.ShouldReportCriticalException(ex))
            {
                telemetry.RecordCriticalException(
                    ex,
                    TelemetryKeys.Feature.Mcp,
                    new Dictionary<string, string> { [TelemetryKeys.Tag.Provider] = tool.Binding.SourceKind.ToString() });
            }

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
            methodResolver.ResolveTool,
            serviceProvider,
            (method, target) => McpServerTool.Create(method, target));
    }

    private static McpToolExecutionResult InvokePythonTool(PythonExecutor executor, McpRegisteredTool tool, string normalizedPayload)
    {
        var binding = tool.Binding;
        if (string.IsNullOrWhiteSpace(binding.SourcePath) || !File.Exists(binding.SourcePath))
            throw new InvalidOperationException($"Python MCP source file was not found: {binding.SourcePath}.");

        var rootFolder = Path.GetDirectoryName(binding.SourcePath) ?? string.Empty;
        var resultJson = executor.Execute(
            binding.SourcePath,
            rootFolder,
            scope =>
            {
                scope.Set(PythonInstances.ToolName, new PyString(tool.ProtocolTool.Name));
                scope.Set(PythonInstances.PayloadJson, new PyString(normalizedPayload));
                scope.Exec(PythonEmbedded.ToolInvokeScript);
                return scope.Get(PythonInstances.ResultJson).As<string>();
            });
        var callResult = PythonResultParser.ParseCallToolResult(resultJson);
        return McpToolExecutionResult.Completed(callResult, $"Completed '{tool.ProtocolTool.Name}'.");
    }

    private Task<McpToolExecutionResult> InvokeCSharpToolAsync(
        string toolName, string normalizedPayload, CancellationToken ct)
    {
        if (!_builtInIndex.TryGetValue(toolName, out var tool))
            return Task.FromResult(McpToolExecutionResult.Failed(
                ExecutionErrorCodes.ToolNotImplemented, $"No C# tool registered for '{toolName}'."));

        return tool.ExecuteAsync(normalizedPayload, ct);
    }

    private static string NormalizePayload(string? payloadJson)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson!);
        return doc.RootElement.ValueKind != JsonValueKind.Object
            ? throw new JsonException("Tool payload must be a JSON object.")
            : doc.RootElement.GetRawText();
    }
}
