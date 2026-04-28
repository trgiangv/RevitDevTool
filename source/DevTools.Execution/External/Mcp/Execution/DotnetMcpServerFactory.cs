using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
namespace DevTools.Execution.External.Mcp.Execution;

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
}
