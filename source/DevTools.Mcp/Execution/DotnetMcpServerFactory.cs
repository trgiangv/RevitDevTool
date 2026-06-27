using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace DevTools.Mcp.Execution;

internal static class DotnetMcpServerFactory
{
    public static TServer? GetOrCreate<TRegistered, TServer>(
        ConcurrentDictionary<string, TServer> cache,
        string cacheKey,
        TRegistered registeredItem,
        Func<TRegistered, MethodInfo?> methodResolver,
        IServiceProvider serviceProvider,
        Func<MethodInfo, object?, TServer> serverFactory)
        where TServer : class
    {
        if (cache.TryGetValue(cacheKey, out var cached))
            return cached;

        var method = methodResolver(registeredItem);
        if (method is null)
            return null;

        var target = method.IsStatic ? null : ActivatorUtilities.CreateInstance(serviceProvider, method.DeclaringType!);
        var server = serverFactory(method, target);
        cache.TryAdd(cacheKey, server);
        return server;
    }

    public static T GetCompletedResult<T>(
        ValueTask<T> task,
        string kind,
        string name)
    {
        if (!task.IsCompleted)
        {
            throw new NotSupportedException(
                $".NET MCP {kind} '{name}' returned an incomplete async task. " +
                "Async .NET MCP handlers are not supported through the synchronous host-context execution path. " +
                "Make the handler complete synchronously, or split the workflow so async work runs outside the host context.");
        }

        return task.GetAwaiter().GetResult();
    }
}
