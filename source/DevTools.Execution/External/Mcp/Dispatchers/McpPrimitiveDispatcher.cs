using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using DevTools.Execution.Providers.Python;
using DevTools.Execution.External.Mcp.Registry;
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
    IEnumerable<IBuiltInMcpResource> builtInResources,
    IEnumerable<IBuiltInMcpPrompt> builtInPrompts,
    ITelemetry telemetry) : IMcpPrimitiveDispatcher
{
    private static readonly JsonSerializerOptions JsonOptions = McpJsonUtilities.DefaultOptions;

    private readonly ConcurrentDictionary<string, McpServerTool> _cachedTools = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, McpServerPrompt> _cachedPrompts = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, McpServerResource> _cachedResources = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, McpServerTool> _builtInToolIndex = builtInTools.ToDictionary(tool => tool.Primitive.ProtocolTool.Name, tool => tool.Primitive, StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, McpServerResource> _builtInResourceIndex = builtInResources.ToDictionary(resource => resource.Primitive.ProtocolResource?.Uri ?? resource.Primitive.ProtocolResourceTemplate.UriTemplate, resource => resource.Primitive, StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, McpServerPrompt> _builtInPromptIndex = builtInPrompts.ToDictionary(prompt => prompt.Primitive.ProtocolPrompt.Name, prompt => prompt.Primitive, StringComparer.OrdinalIgnoreCase);

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
        using var document = JsonDocument.Parse(normalizedPayload);
        var arguments = document.RootElement.EnumerateObject().ToDictionary(property => property.Name, property => property.Value);
        var resultJson = PythonMcpInvoker.InvokeTool(executor, tool, arguments);
        var callResult = PythonResultParser.ParseCallToolResult(resultJson);
        return McpToolExecutionResult.Completed(callResult, $"Completed '{tool.ProtocolTool.Name}'.");
    }

    private Task<McpToolExecutionResult> InvokeCSharpToolAsync(string toolName, string normalizedPayload, CancellationToken ct)
    {
        if (!_builtInToolIndex.TryGetValue(toolName, out var tool))
            return Task.FromResult(McpToolExecutionResult.Failed(
                McpExecutionErrorCodes.ToolNotImplemented, $"No C# tool registered for '{toolName}'."));
        return InvokeBuiltInToolAsync(tool, toolName, normalizedPayload, ct);
    }

    private static async Task<McpToolExecutionResult> InvokeBuiltInToolAsync(McpServerTool tool, string toolName, string normalizedPayload, CancellationToken ct)
    {
        using var document = JsonDocument.Parse(normalizedPayload);
        var requestParams = new CallToolRequestParams
        {
            Name = toolName,
            Arguments = document.RootElement.EnumerateObject().ToDictionary(property => property.Name, property => property.Value)
        };
        var request = RequestContextFactory.Create(requestParams, RequestMethods.ToolsCall);
        var result = await tool.InvokeAsync(request, ct).ConfigureAwait(false);
        return McpToolExecutionResult.Completed(result, $"Completed '{toolName}'.");
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
            ExecutionMode.CSharp => InvokeCSharpPrompt(prompt, arguments),
            _ => throw new InvalidOperationException($"Unsupported prompt execution source '{prompt.Binding.SourceKind}'.")
        };
    }

    private GetPromptResult InvokeCSharpPrompt(McpRegisteredPrompt prompt, IReadOnlyDictionary<string, JsonElement>? arguments)
    {
        if (!_builtInPromptIndex.TryGetValue(prompt.ProtocolPrompt.Name, out var builtIn))
            throw new InvalidOperationException($"No built-in prompt registered for '{prompt.ProtocolPrompt.Name}'.");
        var requestParams = new GetPromptRequestParams
        {
            Name = prompt.ProtocolPrompt.Name,
            Arguments = arguments?.ToDictionary(pair => pair.Key, pair => pair.Value)
        };
        return DotnetMcpServerFactory.GetCompletedResult(
            builtIn.GetAsync(RequestContextFactory.Create(requestParams, RequestMethods.PromptsGet)),
            "prompt", prompt.ProtocolPrompt.Name);
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
        var resultJson = PythonMcpInvoker.InvokePrompt(executor, prompt, arguments ?? new Dictionary<string, JsonElement>());
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
            ExecutionMode.CSharp => InvokeCSharpResource(resource, uri),
            _ => throw new InvalidOperationException($"Unsupported resource execution source '{resource.Binding.SourceKind}'.")
        };
    }

    private ReadResourceResult InvokeCSharpResource(McpRegisteredResource resource, string uri)
    {
        var resourceUri = resource.ProtocolResource?.Uri ?? resource.ProtocolTemplate?.UriTemplate ?? string.Empty;
        if (!_builtInResourceIndex.TryGetValue(resourceUri, out var builtIn))
            throw new InvalidOperationException($"No built-in resource registered for '{resourceUri}'.");
        var requestParams = new ReadResourceRequestParams { Uri = uri };
        return DotnetMcpServerFactory.GetCompletedResult(
            builtIn.ReadAsync(RequestContextFactory.Create(requestParams, RequestMethods.ResourcesRead)),
            "resource", resourceUri);
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
        var resultJson = PythonMcpInvoker.InvokeResource(executor, resource, uri);
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
