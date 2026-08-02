using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Catalog.Discovery;

/// <summary>
/// Host-side service provider for toolset reflection invoke (RequestContext, McpServer, progress, user).
/// </summary>
internal sealed class ToolsetInvocationServices : IServiceProvider, IKeyedServiceProvider,
    IServiceProviderIsService, IServiceProviderIsKeyedService
{
    private readonly RequestContext<CallToolRequestParams> _request;
    private readonly IServiceProvider? _innerServices;

    internal ToolsetInvocationServices(RequestContext<CallToolRequestParams> request)
    {
        _request = request;
        _innerServices = request.Services;
    }

    public object? GetService(Type serviceType) =>
        serviceType == typeof(RequestContext<CallToolRequestParams>) ? _request :
        serviceType == typeof(McpServer) ? _request.Server :
        serviceType == typeof(IProgress<ProgressNotificationValue>) ?
            (_request.Params?.ProgressToken is { } progressToken
                ? new ToolsetProgressReporter(_request.Server, progressToken)
                : ToolsetNopProgress.Instance) :
        serviceType == typeof(ClaimsPrincipal) ? _request.User :
        _innerServices?.GetService(serviceType);

    public bool IsService(Type serviceType) =>
        IsAugmentedWith(serviceType) ||
        (_innerServices as IServiceProviderIsService)?.IsService(serviceType) is true;

    public bool IsKeyedService(Type serviceType, object? serviceKey) =>
        (serviceKey is null && IsService(serviceType)) ||
        (_innerServices as IServiceProviderIsKeyedService)?.IsKeyedService(serviceType, serviceKey) is true;

    public object? GetKeyedService(Type serviceType, object? serviceKey) =>
        serviceKey is null ? GetService(serviceType) :
        (_innerServices as IKeyedServiceProvider)?.GetKeyedService(serviceType, serviceKey);

    public object GetRequiredKeyedService(Type serviceType, object? serviceKey) =>
        GetKeyedService(serviceType, serviceKey) ??
        throw new InvalidOperationException(
            $"No service of type '{serviceType}' with key '{serviceKey}' is registered.");

    internal static bool IsAugmentedWith(Type serviceType) =>
        serviceType == typeof(RequestContext<CallToolRequestParams>) ||
        serviceType == typeof(McpServer) ||
        serviceType == typeof(IProgress<ProgressNotificationValue>) ||
        serviceType == typeof(ClaimsPrincipal);
}

internal sealed class ToolsetProgressReporter : IProgress<ProgressNotificationValue>
{
    private readonly McpServer _server;
    private readonly ProgressToken _progressToken;

    internal ToolsetProgressReporter(McpServer server, ProgressToken progressToken)
    {
        _server = server;
        _progressToken = progressToken;
    }

    public void Report(ProgressNotificationValue value) =>
        _server.NotifyProgressAsync(_progressToken, value, cancellationToken: CancellationToken.None)
            .GetAwaiter().GetResult();
}

internal sealed class ToolsetNopProgress : IProgress<ProgressNotificationValue>
{
    internal static ToolsetNopProgress Instance { get; } = new();

    public void Report(ProgressNotificationValue value) { }
}
