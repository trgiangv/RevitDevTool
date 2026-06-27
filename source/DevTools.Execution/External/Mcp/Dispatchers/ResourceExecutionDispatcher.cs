using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using DevTools.Execution.External.Mcp.Execution;
using DevTools.Execution.Providers.Python;
using DevTools.McpParser.Models;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Python.Runtime;
namespace DevTools.Execution.External.Mcp.Dispatchers;

/// <summary>
/// Dispatches MCP resource reads asynchronously via the dotnet backend,
/// or synchronously under the GIL for the Python backend.
/// </summary>
public sealed class ResourceExecutionDispatcher(
    IServiceProvider serviceProvider, PythonExecutor executor, DotnetMethodResolver methodResolver) : ICacheable
{
    private static readonly JsonSerializerOptions JsonOptions = McpJsonUtilities.DefaultOptions;
    private readonly ConcurrentDictionary<string, McpServerResource> _cachedResources = new(StringComparer.OrdinalIgnoreCase);

    public ReadResourceResult ReadResource(
        McpRegisteredResource resource,
        string uri,
        CancellationToken ct = default)
    {
        // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
        return resource.Binding.SourceKind switch
        {
            ExecutionMode.Assembly => InvokeDotnetResource(resource, uri, ct),
            ExecutionMode.Python => InvokePythonResource(resource, uri),
            _ => throw new InvalidOperationException(
                $"Unsupported resource execution source '{resource.Binding.SourceKind}'.")
        };
    }

    public void ClearCache() => _cachedResources.Clear();

    private ReadResourceResult InvokeDotnetResource(
        McpRegisteredResource resource,
        string uri,
        CancellationToken ct)
    {
        var serverResource = GetOrCreateServerResource(resource);
        if (serverResource is null)
        {
            throw new InvalidOperationException(
                $"No .NET resource method mapped for '{resource.ProtocolResource?.Name ?? resource.ProtocolTemplate?.Name}'.");
        }

        var requestParams = new ReadResourceRequestParams { Uri = uri };

        var requestContext = RequestContextFactory.Create(
            requestParams,
            RequestMethods.ResourcesRead);

        var name = resource.ProtocolResource?.Name
                   ?? resource.ProtocolTemplate?.Name
                   ?? uri;

        return DotnetMcpServerFactory.GetCompletedResult(
            serverResource.ReadAsync(requestContext, ct),
            "resource",
            name);
    }

    private McpServerResource? GetOrCreateServerResource(McpRegisteredResource resource)
    {
        return DotnetMcpServerFactory.GetOrCreate(
            _cachedResources,
            resource.Id,
            resource,
            methodResolver.ResolveResource,
            serviceProvider,
            (method, target) => McpServerResource.Create(method, target));
    }

    private ReadResourceResult InvokePythonResource(McpRegisteredResource resource, string uri)
    {
        var sourcePath = resource.Binding.SourcePath;
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            throw new InvalidOperationException($"Python MCP source file was not found: {sourcePath}.");

        var rootFolder = Path.GetDirectoryName(sourcePath) ?? string.Empty;
        var resultJson = executor.Execute(
            sourcePath,
            rootFolder,
            scope =>
            {
                var name = resource.ProtocolResource?.Name ?? resource.ProtocolTemplate?.Name ?? string.Empty;
                scope.Set(PythonInstances.Operation, new PyString(PythonInstances.OperationResource));
                scope.Set(PythonInstances.ResourceName, new PyString(name));
                scope.Set(PythonInstances.ResourceUri, new PyString(uri));
                scope.Exec(PythonEmbedded.ToolInvokeScript);
                return scope.Get(PythonInstances.ResultJson).As<string>();
            });

        return JsonSerializer.Deserialize<ReadResourceResult>(resultJson, JsonOptions)
               ?? new ReadResourceResult();
    }
}
