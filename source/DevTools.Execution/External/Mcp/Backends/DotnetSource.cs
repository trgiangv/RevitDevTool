using System.Reflection;
using System.Collections.Concurrent;
using DevTools.Mcp.Bridging;
using DevTools.Mcp.Discovery;
using DevTools.Mcp.Hosting;
using DevTools.Mcp.Core.Models;
using DevTools.Mcp.Core.Utils;
using DevTools.Execution.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Execution.External.Mcp.Backends;

/// <summary>Invokes isolated .NET MCP toolsets and owns their reflection caches.</summary>
public sealed class DotnetSource(
    IServiceProvider serviceProvider,
    DotnetMethodResolver methodResolver) : IMcpSource
{
    private readonly ConcurrentDictionary<string, McpServerTool> _tools =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, McpServerResource> _resources =
        new(StringComparer.OrdinalIgnoreCase);

    public ExecutionMode SourceKind => ExecutionMode.Dotnet;

    public McpServerTool CreateTool(RegisteredTool tool, IHostContextExecutor hostContext) =>
        SdkCollectionTool.Create(
            tool.Descriptor,
            (request, cancellationToken) => InvokeToolAsync(
                tool,
                request.Params ?? new CallToolRequestParams { Name = tool.Descriptor.Name },
                hostContext,
                cancellationToken));

    public McpServerResource CreateResource(RegisteredResource resource, IHostContextExecutor hostContext) =>
        SdkCollectionResource.Create(resource, hostContext, (uri, cancellationToken) => ReadResourceAsync(resource, uri, cancellationToken));

    public async Task<CallToolResult> InvokeToolAsync(
        RegisteredTool tool,
        CallToolRequestParams request,
        IHostContextExecutor hostContext,
        CancellationToken cancellationToken)
    {
        return await hostContext.ExecuteAsync(
            () =>
            {
                var serverTool = GetOrCreateTool(tool);
                if (serverTool is null)
                    return ToolHelpers.ErrorResult($"No .NET tool method mapped for '{tool.Descriptor.Name}'.");

                var context = RequestFactory.ToToolContext(tool.Descriptor.Name, request);
                var pending = serverTool.InvokeAsync(context, cancellationToken);
                if (!pending.IsCompleted)
                {
                    throw new NotSupportedException(
                        $".NET MCP tool '{tool.Descriptor.Name}' returned an incomplete task. The host dispatcher runs synchronous delegates only.");
                }

                return ResultBridge.ToHostCallToolResult(pending.GetAwaiter().GetResult(), tool.Descriptor.OutputSchema);
            },
            cancellationToken).ConfigureAwait(false);
    }

    private McpServerTool? GetOrCreateTool(RegisteredTool tool)
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

    public async Task<ReadResourceResult> ReadResourceAsync(
        RegisteredResource resource,
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

        return await serverResource.ReadAsync(request, cancellationToken).AsTask().ConfigureAwait(false);
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
}
