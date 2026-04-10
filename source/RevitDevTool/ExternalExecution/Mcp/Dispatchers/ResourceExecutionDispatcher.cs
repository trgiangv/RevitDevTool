using System.Collections.Concurrent;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Python.Runtime;
using RevitDevTool.Execution.Providers.Python;
using RevitDevTool.ExternalExecution.Mcp.Execution;
using RevitDevTool.McpParser;
using RevitDevTool.McpParser.Dotnet;
using RevitDevTool.McpParser.Models;

namespace RevitDevTool.ExternalExecution.Mcp.Dispatchers;

/// <summary>
/// Dispatches MCP resource reads asynchronously via the dotnet backend,
/// or synchronously under the GIL for the Python backend.
/// </summary>
public sealed class ResourceExecutionDispatcher(
    IServiceProvider serviceProvider,
    PythonInitializer pythonInitializer) : ICacheable
{
    private static readonly JsonSerializerOptions JsonOptions = McpJsonUtilities.DefaultOptions;
    private readonly ConcurrentDictionary<string, McpServerResource> _cachedResources = new(StringComparer.OrdinalIgnoreCase);

    public async Task<ReadResourceResult> ReadResourceAsync(
        McpRegisteredResource resource,
        string uri)
    {
        return resource.Binding.SourceKind switch
        {
            ExecutionMode.Assembly => await InvokeDotnetResourceAsync(resource, uri).ConfigureAwait(false),
            ExecutionMode.Python => InvokePythonResource(resource, uri),
            _ => throw new InvalidOperationException($"Unsupported resource execution source '{resource.Binding.SourceKind}'.")
        };
    }

    public void ClearCache() => _cachedResources.Clear();

    private async Task<ReadResourceResult> InvokeDotnetResourceAsync(
        McpRegisteredResource resource,
        string uri)
    {
        var serverResource = GetOrCreateServerResource(resource);
        if (serverResource is null)
            throw new InvalidOperationException($"No .NET resource method mapped for '{resource.ProtocolResource?.Name ?? resource.ProtocolTemplate?.Name}'.");

        var requestParams = new ReadResourceRequestParams { Uri = uri };
        var requestContext = RequestContextFactory.Create(requestParams, RequestMethods.ResourcesRead);
        return await serverResource.ReadAsync(requestContext, CancellationToken.None).ConfigureAwait(false);
    }

    private McpServerResource? GetOrCreateServerResource(McpRegisteredResource resource)
    {
        return DotnetMcpServerFactory.GetOrCreate(
            _cachedResources,
            resource.Id,
            resource,
            DotnetMethodResolver.ResolveResource,
            serviceProvider,
            (method, target) => McpServerResource.Create(method, target));
    }

    private ReadResourceResult InvokePythonResource(McpRegisteredResource resource, string uri)
    {
        var resultJson = PythonExecutionHelper.InvokeScript(pythonInitializer,
            resource.Binding.SourcePath,
            scope =>
            {
                var name = resource.ProtocolResource?.Name ?? resource.ProtocolTemplate?.Name ?? string.Empty;
                scope.Set(PythonScopeVars.Operation, new PyString(PythonScopeVars.OperationResource));
                scope.Set(PythonScopeVars.ResourceName, new PyString(name));
                scope.Set(PythonScopeVars.ResourceUri, new PyString(uri));
            });

        return JsonSerializer.Deserialize<ReadResourceResult>(resultJson, JsonOptions)
               ?? new ReadResourceResult();
    }
}
