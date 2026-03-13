using System.Collections.Concurrent;
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

public sealed class PrimitiveExecutionDispatcher(IServiceProvider serviceProvider)
{
    private static readonly JsonSerializerOptions JsonOptions = McpJsonUtilities.DefaultOptions;
    private readonly ConcurrentDictionary<string, McpServerPrompt> _cachedPrompts = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, McpServerResource> _cachedResources = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Runs inside Revit's <c>IExternalEventHandler.Execute()</c> — synchronous by design.
    /// </summary>
    public GetPromptResult GetPrompt(
        McpRegisteredPrompt prompt,
        IReadOnlyDictionary<string, JsonElement>? arguments)
    {
        return prompt.Binding.SourceKind switch
        {
            ExecutionMode.Assembly => InvokeDotnetPrompt(prompt, arguments),
            ExecutionMode.Python => InvokePythonPrompt(prompt, arguments),
            _ => throw new InvalidOperationException($"Unsupported prompt execution source '{prompt.Binding.SourceKind}'.")
        };
    }

    /// <summary>
    /// Runs inside Revit's <c>IExternalEventHandler.Execute()</c> — synchronous by design.
    /// </summary>
    public ReadResourceResult ReadResource(
        McpRegisteredResource resource,
        string uri)
    {
        return resource.Binding.SourceKind switch
        {
            ExecutionMode.Assembly => InvokeDotnetResource(resource, uri),
            ExecutionMode.Python => InvokePythonResource(resource, uri),
            _ => throw new InvalidOperationException($"Unsupported resource execution source '{resource.Binding.SourceKind}'.")
        };
    }

    private GetPromptResult InvokeDotnetPrompt(
        McpRegisteredPrompt prompt,
        IReadOnlyDictionary<string, JsonElement>? arguments)
    {
        var serverPrompt = GetOrCreateServerPrompt(prompt);
        if (serverPrompt is null)
            throw new InvalidOperationException($"No .NET prompt method mapped for '{prompt.ProtocolPrompt.Name}'.");

        var requestParams = new GetPromptRequestParams
        {
            Name = prompt.ProtocolPrompt.Name,
            Arguments = arguments?.ToDictionary(kv => kv.Key, kv => kv.Value)
        };
        var requestContext = RequestContextFactory.Create(requestParams, RequestMethods.PromptsGet);
        return serverPrompt.GetAsync(requestContext, CancellationToken.None)
            .ConfigureAwait(false).GetAwaiter().GetResult();
    }

    private ReadResourceResult InvokeDotnetResource(
        McpRegisteredResource resource,
        string uri)
    {
        var serverResource = GetOrCreateServerResource(resource);
        if (serverResource is null)
            throw new InvalidOperationException($"No .NET resource method mapped for '{resource.ProtocolResource?.Name ?? resource.ProtocolTemplate?.Name}'.");

        var requestParams = new ReadResourceRequestParams { Uri = uri };
        var requestContext = RequestContextFactory.Create(requestParams, RequestMethods.ResourcesRead);
        return serverResource.ReadAsync(requestContext, CancellationToken.None)
            .ConfigureAwait(false).GetAwaiter().GetResult();
    }

    private McpServerPrompt? GetOrCreateServerPrompt(McpRegisteredPrompt prompt)
    {
        if (_cachedPrompts.TryGetValue(prompt.Id, out var cached))
            return cached;

        var method = DotnetMcpPrimitiveMethodResolver.ResolvePrompt(prompt);
        if (method is null)
            return null;

        var target = method.IsStatic ? null : ActivatorUtilities.CreateInstance(serviceProvider, method.DeclaringType!);
        var serverPrompt = McpServerPrompt.Create(method, target);
        _cachedPrompts.TryAdd(prompt.Id, serverPrompt);
        return serverPrompt;
    }

    private McpServerResource? GetOrCreateServerResource(McpRegisteredResource resource)
    {
        if (_cachedResources.TryGetValue(resource.Id, out var cached))
            return cached;

        var method = DotnetMcpPrimitiveMethodResolver.ResolveResource(resource);
        if (method is null)
            return null;

        var target = method.IsStatic ? null : ActivatorUtilities.CreateInstance(serviceProvider, method.DeclaringType!);
        var serverResource = McpServerResource.Create(method, target);
        _cachedResources.TryAdd(resource.Id, serverResource);
        return serverResource;
    }

    private static GetPromptResult InvokePythonPrompt(
        McpRegisteredPrompt prompt,
        IReadOnlyDictionary<string, JsonElement>? arguments)
    {
        var resultJson = InvokePythonPrimitive(
            prompt.Binding.SourcePath,
            scope =>
            {
                scope.Set("__operation__", new PyString("prompt"));
                scope.Set("__prompt_name__", new PyString(prompt.ProtocolPrompt.Name));
                scope.Set("__arguments_json__", new PyString(SerializeJson(arguments ?? new Dictionary<string, JsonElement>())));
            });

        return JsonSerializer.Deserialize<GetPromptResult>(resultJson, JsonOptions)
               ?? new GetPromptResult { Description = prompt.ProtocolPrompt.Description };
    }

    private static ReadResourceResult InvokePythonResource(McpRegisteredResource resource, string uri)
    {
        var resultJson = InvokePythonPrimitive(
            resource.Binding.SourcePath,
            scope =>
            {
                var name = resource.ProtocolResource?.Name ?? resource.ProtocolTemplate?.Name ?? string.Empty;
                scope.Set("__operation__", new PyString("resource"));
                scope.Set("__resource_name__", new PyString(name));
                scope.Set("__resource_uri__", new PyString(uri));
            });

        return JsonSerializer.Deserialize<ReadResourceResult>(resultJson, JsonOptions)
               ?? new ReadResourceResult();
    }

    private static string SerializeJson<T>(T value)
    {
        return JsonSerializer.Serialize(value, JsonOptions);
    }

    private static string InvokePythonPrimitive(string sourcePath, Action<PyModule> configureScope)
    {
        PythonInitializer.InitializeAsync().GetAwaiter().GetResult();

        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            throw new InvalidOperationException($"Python MCP source file was not found: {sourcePath}.");

        using (Py.GIL())
        {
            if (PythonInitializer.GlobalScope is null)
                throw new InvalidOperationException("Global Python scope not initialized.");

            using var scope = PythonInitializer.GlobalScope.NewScope();
            PythonExecutor.PrepareExecutionScope(scope, sourcePath);
            configureScope(scope);
            scope.Exec(PythonEmbedded.ToolInvokeScript);
            return scope.Get("__result_json__").As<string>();
        }
    }
}
