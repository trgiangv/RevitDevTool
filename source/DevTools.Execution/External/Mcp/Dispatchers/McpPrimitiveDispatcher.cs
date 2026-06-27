using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using DevTools.Execution.Providers.Python;
using DevTools.Telemetry;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Python.Runtime;
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace DevTools.Execution.External.Mcp.Dispatchers;

/// <summary>
/// Unified dispatcher for all MCP primitive invocations (tools, prompts, resources).
/// Routes to .NET assembly, Python, or built-in C# execution backends.
/// </summary>
public sealed class McpPrimitiveDispatcher(
    IServiceProvider serviceProvider,
    PythonExecutor executor,
    DotnetMethodResolver methodResolver,
    IEnumerable<IBuiltInMcpTool> builtInTools,
    ITelemetry telemetry) : IMcpPrimitiveDispatcher
{
    private static readonly JsonSerializerOptions JsonOptions = McpJsonUtilities.DefaultOptions;

    private readonly ConcurrentDictionary<string, McpServerTool> _cachedTools = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, McpServerPrompt> _cachedPrompts = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, McpServerResource> _cachedResources = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IBuiltInMcpTool> _builtInIndex = builtInTools.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);

    #region Tools

    public async Task<McpToolExecutionResult> DispatchToolAsync(
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
                ExecutionMode.Dotnet => await hostContext
                    .ExecuteAsync(() => InvokeDotnetTool(tool, normalizedPayload, ct), ct)
                    .ConfigureAwait(false),
                ExecutionMode.Python => await hostContext
                    .ExecuteAsync(() => InvokePythonTool(tool, normalizedPayload), ct)
                    .ConfigureAwait(false),
                ExecutionMode.CSharp => await InvokeCSharpToolAsync(tool.ProtocolTool.Name, normalizedPayload, ct)
                    .ConfigureAwait(false),
                _ => McpToolExecutionResult.Failed(McpExecutionErrorCodes.ToolUnknownSourceKind,
                    $"Unknown or unsupported MCP tool execution: '{tool.Binding.SourceKind}'.")
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

            return McpToolExecutionResult.Failed(McpExecutionErrorCodes.ToolInvokeFailed, ex.Message, ex.StackTrace);
        }
    }

    private McpToolExecutionResult InvokeDotnetTool(McpRegisteredTool tool, string normalizedPayload, CancellationToken ct)
    {
        var serverTool = DotnetMcpServerFactory.GetOrCreate(
            _cachedTools, tool.Id, tool, methodResolver.ResolveTool, serviceProvider,
            (method, target) => McpServerTool.Create(method, target));

        if (serverTool is null)
            return McpToolExecutionResult.Failed(McpExecutionErrorCodes.ToolNotImplemented,
                $"No .NET tool method mapped for '{tool.ProtocolTool.Name}'.");

        using var doc = JsonDocument.Parse(normalizedPayload);
        var arguments = new Dictionary<string, JsonElement>();
        foreach (var prop in doc.RootElement.EnumerateObject())
            arguments[prop.Name] = prop.Value;

        var requestParams = new CallToolRequestParams { Name = tool.ProtocolTool.Name, Arguments = arguments };
        var requestContext = RequestContextFactory.Create(requestParams, RequestMethods.ToolsCall);
        var result = DotnetMcpServerFactory.GetCompletedResult(serverTool.InvokeAsync(requestContext, ct), "tool", tool.ProtocolTool.Name);
        return McpToolExecutionResult.Completed(result, $"Completed '{tool.ProtocolTool.Name}'.");
    }

    private McpToolExecutionResult InvokePythonTool(McpRegisteredTool tool, string normalizedPayload)
    {
        var binding = tool.Binding;
        if (string.IsNullOrWhiteSpace(binding.SourcePath) || !File.Exists(binding.SourcePath))
            throw new InvalidOperationException($"Python MCP source file was not found: {binding.SourcePath}.");

        var rootFolder = Path.GetDirectoryName(binding.SourcePath) ?? string.Empty;
        var resultJson = executor.Execute(
            binding.SourcePath, rootFolder,
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

    private Task<McpToolExecutionResult> InvokeCSharpToolAsync(string toolName, string normalizedPayload, CancellationToken ct)
    {
        if (!_builtInIndex.TryGetValue(toolName, out var tool))
            return Task.FromResult(McpToolExecutionResult.Failed(
                McpExecutionErrorCodes.ToolNotImplemented, $"No C# tool registered for '{toolName}'."));
        return tool.ExecuteAsync(normalizedPayload, ct);
    }

    #endregion

    #region Prompts

    public GetPromptResult GetPrompt(
        McpRegisteredPrompt prompt,
        IReadOnlyDictionary<string, JsonElement>? arguments,
        CancellationToken ct = default)
    {
        return prompt.Binding.SourceKind switch
        {
            ExecutionMode.Dotnet => InvokeDotnetPrompt(prompt, arguments, ct),
            ExecutionMode.Python => InvokePythonPrompt(prompt, arguments),
            _ => throw new InvalidOperationException($"Unsupported prompt execution source '{prompt.Binding.SourceKind}'.")
        };
    }

    private GetPromptResult InvokeDotnetPrompt(McpRegisteredPrompt prompt, IReadOnlyDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        var serverPrompt = DotnetMcpServerFactory.GetOrCreate(
            _cachedPrompts, prompt.Id, prompt, methodResolver.ResolvePrompt, serviceProvider,
            (method, target) => McpServerPrompt.Create(method, target));

        if (serverPrompt is null)
            throw new InvalidOperationException($"No .NET prompt method mapped for '{prompt.ProtocolPrompt.Name}'.");

        var requestParams = new GetPromptRequestParams
        {
            Name = prompt.ProtocolPrompt.Name,
            Arguments = arguments?.ToDictionary(kv => kv.Key, kv => kv.Value)
        };
        var requestContext = RequestContextFactory.Create(requestParams, RequestMethods.PromptsGet);
        return DotnetMcpServerFactory.GetCompletedResult(serverPrompt.GetAsync(requestContext, ct), "prompt", prompt.ProtocolPrompt.Name);
    }

    private GetPromptResult InvokePythonPrompt(McpRegisteredPrompt prompt, IReadOnlyDictionary<string, JsonElement>? arguments)
    {
        var sourcePath = prompt.Binding.SourcePath;
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            throw new InvalidOperationException($"Python MCP source file was not found: {sourcePath}.");

        var rootFolder = Path.GetDirectoryName(sourcePath) ?? string.Empty;
        var resultJson = executor.Execute(
            sourcePath, rootFolder,
            scope =>
            {
                scope.Set(PythonInstances.Operation, new PyString(PythonInstances.OperationPrompt));
                scope.Set(PythonInstances.PromptName, new PyString(prompt.ProtocolPrompt.Name));
                scope.Set(PythonInstances.ArgumentsJson, new PyString(SerializeJson(arguments ?? new Dictionary<string, JsonElement>())));
                scope.Exec(PythonEmbedded.ToolInvokeScript);
                return scope.Get(PythonInstances.ResultJson).As<string>();
            });
        return JsonSerializer.Deserialize<GetPromptResult>(resultJson, JsonOptions)
               ?? new GetPromptResult { Description = prompt.ProtocolPrompt.Description };
    }

    #endregion

    #region Resources

    public ReadResourceResult ReadResource(
        McpRegisteredResource resource,
        string uri,
        CancellationToken ct = default)
    {
        return resource.Binding.SourceKind switch
        {
            ExecutionMode.Dotnet => InvokeDotnetResource(resource, uri, ct),
            ExecutionMode.Python => InvokePythonResource(resource, uri),
            _ => throw new InvalidOperationException($"Unsupported resource execution source '{resource.Binding.SourceKind}'.")
        };
    }

    private ReadResourceResult InvokeDotnetResource(McpRegisteredResource resource, string uri, CancellationToken ct)
    {
        var serverResource = DotnetMcpServerFactory.GetOrCreate(
            _cachedResources, resource.Id, resource, methodResolver.ResolveResource, serviceProvider,
            (method, target) => McpServerResource.Create(method, target));

        if (serverResource is null)
            throw new InvalidOperationException(
                $"No .NET resource method mapped for '{resource.ProtocolResource?.Name ?? resource.ProtocolTemplate?.Name}'.");

        var requestParams = new ReadResourceRequestParams { Uri = uri };
        var requestContext = RequestContextFactory.Create(requestParams, RequestMethods.ResourcesRead);
        var name = resource.ProtocolResource?.Name ?? resource.ProtocolTemplate?.Name ?? uri;
        return DotnetMcpServerFactory.GetCompletedResult(serverResource.ReadAsync(requestContext, ct), "resource", name);
    }

    private ReadResourceResult InvokePythonResource(McpRegisteredResource resource, string uri)
    {
        var sourcePath = resource.Binding.SourcePath;
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            throw new InvalidOperationException($"Python MCP source file was not found: {sourcePath}.");

        var rootFolder = Path.GetDirectoryName(sourcePath) ?? string.Empty;
        var resultJson = executor.Execute(
            sourcePath, rootFolder,
            scope =>
            {
                var name = resource.ProtocolResource?.Name ?? resource.ProtocolTemplate?.Name ?? string.Empty;
                scope.Set(PythonInstances.Operation, new PyString(PythonInstances.OperationResource));
                scope.Set(PythonInstances.ResourceName, new PyString(name));
                scope.Set(PythonInstances.ResourceUri, new PyString(uri));
                scope.Exec(PythonEmbedded.ToolInvokeScript);
                return scope.Get(PythonInstances.ResultJson).As<string>();
            });
        return JsonSerializer.Deserialize<ReadResourceResult>(resultJson, JsonOptions) ?? new ReadResourceResult();
    }

    #endregion

    public void ClearCaches()
    {
        _cachedTools.Clear();
        _cachedPrompts.Clear();
        _cachedResources.Clear();
    }

    private static string NormalizePayload(string? payloadJson)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson!);
        return doc.RootElement.ValueKind != JsonValueKind.Object
            ? throw new JsonException("Tool payload must be a JSON object.")
            : doc.RootElement.GetRawText();
    }

    private static string SerializeJson<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);
}
