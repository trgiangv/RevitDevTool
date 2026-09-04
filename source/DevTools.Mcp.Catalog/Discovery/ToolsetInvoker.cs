using System.Reflection;
using System.Text.Json;
using DevTools.Mcp.Core.Invocation;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Catalog.Discovery;

/// <summary>
/// Reflection invoke for isolated .NET toolsets with JSON boundary at <see cref="McpInvocationResponse"/>.
/// </summary>
public static class ToolsetInvoker
{
    public static McpInvocationResponse InvokeToResponse(
        MethodInfo method,
        object? target,
        RequestContext<CallToolRequestParams> request,
        IServiceProvider serviceProvider,
        JsonElement? outputSchema,
        CancellationToken cancellationToken)
    {
        var raw = InvokeRaw(method, target, request, serviceProvider, cancellationToken);
        return ToolsetResultSerializer.ToInvocationResponse(raw, outputSchema);
    }

    public static object? InvokeRaw(
        MethodInfo method,
        object? target,
        RequestContext<CallToolRequestParams> request,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var args = ToolsetArgumentBinder.Bind(method, request, serviceProvider, cancellationToken);

        object? result;
        try
        {
            result = method.Invoke(target, args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            if (ToolsetMrtrBridge.IsIsolatedInputRequired(ex.InnerException))
                throw ToolsetMrtrBridge.ToHostException(ex.InnerException);
            throw ex.InnerException;
        }

        if (result is not Task task) return result;
        
        if (!task.IsCompleted)
        {
            throw new NotSupportedException(
                "Isolated .NET MCP tool returned an incomplete async task. " +
                "Toolset handlers must complete synchronously on the host context thread.");
        }

        if (method.ReturnType.IsGenericType &&
            method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            return task.GetType().GetProperty("Result")?.GetValue(task);
        }

        return null;

    }
}
