using System.Collections.Concurrent;
using System.Reflection;
using DevTools.Mcp.Catalog.Bridging;
using DevTools.Mcp.Catalog.Discovery;
using DevTools.Mcp.Core.Invocation;
using DevTools.Mcp.Core.Models;
using DevTools.Mcp.Core.Results;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Execution.External.Mcp.Backends;

/// <summary>Invokes isolated .NET MCP toolsets and owns their reflection caches.</summary>
public sealed class DotnetMcpToolBackend(
    IServiceProvider serviceProvider,
    DotnetMethodResolver methodResolver) : IMcpPrimitiveBackend
{
    private readonly ConcurrentDictionary<string, McpServerTool> _tools =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, McpServerResource> _resources =
        new(StringComparer.OrdinalIgnoreCase);

    public ExecutionMode SourceKind => ExecutionMode.Dotnet;

    public async Task<McpResult<McpInvocationResponse>> InvokeToolAsync(
        McpRegisteredTool tool,
        CallToolRequestParams request,
        IHostContextExecutor hostContext,
        CancellationToken cancellationToken)
    {
        return await hostContext.ExecuteAsync(
            () => Invoke(tool, request, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    private McpResult<McpInvocationResponse> Invoke(
        McpRegisteredTool tool,
        CallToolRequestParams request,
        CancellationToken cancellationToken)
    {
        var serverTool = GetOrCreateTool(tool);
        if (serverTool is null)
        {
            return McpResult<McpInvocationResponse>.Failure(new McpError(
                McpErrorCode.ExecutionFailed,
                $"No .NET tool method mapped for '{tool.Descriptor.Name}'.",
                []));
        }

        var context = RequestFactory.ToToolContext(tool.Descriptor.Name, request);
        var task = serverTool.InvokeAsync(context, cancellationToken);
        if (!task.IsCompleted)
            throw new NotSupportedException(
                $".NET MCP tool '{tool.Descriptor.Name}' returned an incomplete async task on the synchronous host path.");
        var raw = task.GetAwaiter().GetResult();
        return McpResult<McpInvocationResponse>.Success(
            ToolsetResultSerializer.ToInvocationResponse(raw, tool.Descriptor.OutputSchema));
    }

    private McpServerTool? GetOrCreateTool(McpRegisteredTool tool)
    {
        if (_tools.TryGetValue(tool.Id, out var cached))
            return cached;

        var method = methodResolver.ResolveTool(tool);
        if (method is null)
            return null;

        var target = method.IsStatic
            ? null
            : ActivatorUtilities.CreateInstance(serviceProvider, method.DeclaringType!);
        var serverTool = McpServerTool.Create(
            method,
            target,
            McpCatalogCreateOptions.ForTool(tool, serviceProvider));
        _tools.TryAdd(tool.Id, serverTool);
        return serverTool;
    }

    public ReadResourceResult ReadResource(
        McpRegisteredResource resource,
        string uri,
        CancellationToken cancellationToken)
    {
        var request = RequestFactory.ToResourceContext(uri);
        var serverResource = GetOrCreate(
            _resources,
            resource.Id,
            resource,
            methodResolver.ResolveResource,
            (method, target) => McpServerResource.Create(
                method,
                target,
                McpCatalogCreateOptions.ForResource(resource, serviceProvider)));

        if (serverResource is null)
            throw new InvalidOperationException($"No .NET resource method mapped for '{resource.DisplayName}'.");

        return GetCompletedResult(
            serverResource.ReadAsync(request, cancellationToken),
            "resource",
            resource.DisplayName);
    }

    public void ClearCaches()
    {
        _tools.Clear();
        _resources.Clear();
    }

    private TServer? GetOrCreate<TRegistered, TServer>(
        ConcurrentDictionary<string, TServer> cache,
        string cacheKey,
        TRegistered registeredItem,
        Func<TRegistered, MethodInfo?> resolver,
        Func<MethodInfo, object?, TServer> factory)
        where TServer : class
    {
        if (cache.TryGetValue(cacheKey, out var cached))
            return cached;

        var method = resolver(registeredItem);
        if (method is null)
            return null;
        var target = method.IsStatic
            ? null
            : ActivatorUtilities.CreateInstance(serviceProvider, method.DeclaringType!);
        var server = factory(method, target);
        cache.TryAdd(cacheKey, server);
        return server;
    }

    private static T GetCompletedResult<T>(ValueTask<T> task, string kind, string name)
    {
        if (!task.IsCompleted)
        {
            throw new NotSupportedException(
                $".NET MCP {kind} '{name}' returned an incomplete async task. " +
                "Async handlers are unsupported on the synchronous host-context path.");
        }

        return task.GetAwaiter().GetResult();
    }
}
