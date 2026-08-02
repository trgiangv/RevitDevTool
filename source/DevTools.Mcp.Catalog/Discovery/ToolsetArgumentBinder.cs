using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Catalog.Discovery;

/// <summary>Binds toolset method parameters from MCP call context without SDK <see cref="Microsoft.Extensions.AI.AIFunction"/>.</summary>
internal static class ToolsetArgumentBinder
{
    private static readonly JsonSerializerOptions JsonOptions = McpJsonUtilities.DefaultOptions;

    public static object?[] Bind(
        MethodInfo method,
        RequestContext<CallToolRequestParams> request,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var invocationServices = new ToolsetInvocationServices(request);
        var parameters = method.GetParameters();
        var values = new object?[parameters.Length];

        for (var i = 0; i < parameters.Length; i++)
            values[i] = BindParameter(parameters[i], request, invocationServices, serviceProvider, cancellationToken);

        return values;
    }

    private static object? BindParameter(
        ParameterInfo parameter,
        RequestContext<CallToolRequestParams> request,
        ToolsetInvocationServices invocationServices,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var parameterType = parameter.ParameterType;

        if (parameterType == typeof(CancellationToken))
            return cancellationToken;

        if (ToolsetInvocationServices.IsAugmentedWith(parameterType) ||
            (serviceProvider.GetService<IServiceProviderIsService>() is { } isService &&
             isService.IsService(parameterType)))
        {
            return invocationServices.GetService(parameterType) ??
                   serviceProvider.GetService(parameterType) ??
                   (parameter.HasDefaultValue
                       ? parameter.DefaultValue
                       : throw new ArgumentException(
                           $"No service of type '{parameterType}' was registered for tool parameter '{parameter.Name}'."));
        }

        if (string.Equals(parameter.Name, "hasInputResponses", StringComparison.Ordinal) &&
            parameterType == typeof(bool))
        {
            return request.Params.InputResponses is { Count: > 0 };
        }

        if (request.Params.Arguments is { } arguments &&
            parameter.Name is not null &&
            arguments.TryGetValue(parameter.Name, out var element))
        {
            return DeserializeArgument(element, parameterType);
        }

        if (parameter.HasDefaultValue)
            return parameter.DefaultValue;

        throw new ArgumentException(
            $"Required tool parameter '{parameter.Name}' was not provided in the MCP call arguments.");
    }

    private static object? DeserializeArgument(JsonElement element, Type parameterType)
    {
        if (element.ValueKind is JsonValueKind.Null)
            return parameterType.IsValueType ? Activator.CreateInstance(parameterType) : null;

        return element.Deserialize(parameterType, JsonOptions);
    }
}
