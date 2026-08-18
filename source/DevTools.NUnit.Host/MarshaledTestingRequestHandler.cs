using System.Text.Json;
using DevTools.Execution.Abstractions;
using DevTools.Hosting;
using DevTools.Ipc;
using DevTools.Testing.Host;
using DevTools.Testing.Transport;

namespace DevTools.NUnit.Host;

/// <summary>
/// Host-thread wrapper around <see cref="TestingRequestHandler"/>.
/// <c>testing/run</c> must not execute on the pipe thread.
/// This is the sole in-host testing protocol surface.
/// </summary>
public sealed class MarshaledTestingRequestHandler : IBridgeRequestHandler, IBridgeNotificationPublisher
{
    readonly TestingRequestHandler _inner;
    readonly IHostContextExecutor _hostContext;

    public MarshaledTestingRequestHandler(
        TestingProviderRegistry registry,
        IHostAppInfo hostInfo,
        IHostContextExecutor hostContext)
    {
        if (registry is null)
            throw new ArgumentNullException(nameof(registry));
        if (hostInfo is null)
            throw new ArgumentNullException(nameof(hostInfo));

        _inner = new TestingRequestHandler(
            registry,
            hostInfo.Host.ToString(),
            hostInfo.VersionNumber);
        _hostContext = hostContext ?? throw new ArgumentNullException(nameof(hostContext));
    }

    public IReadOnlyCollection<string> SupportedMethods => _inner.SupportedMethods;

    public Action<string, JsonElement?>? NotificationSender
    {
        get => _inner.NotificationSender;
        set => _inner.NotificationSender = value;
    }

    public Task<BridgeMessage> HandleAsync(
        string requestId,
        string method,
        JsonElement? @params,
        CancellationToken ct = default)
    {
        if (string.Equals(method, TestingProtocol.Run, StringComparison.OrdinalIgnoreCase))
            return HandleRunAsync(requestId, method, @params, ct);

        return _inner.HandleAsync(requestId, method, @params, ct);
    }

    async Task<BridgeMessage> HandleRunAsync(
        string requestId,
        string method,
        JsonElement? @params,
        CancellationToken ct)
    {
        ExecutionGuardContext.Mode = ExecutionGuardMode.Suppress;
        return await _hostContext
            .ExecuteAsync(
                () => _inner.HandleAsync(requestId, method, @params, ct).GetAwaiter().GetResult(),
                ct)
            .ConfigureAwait(false);
    }
}
