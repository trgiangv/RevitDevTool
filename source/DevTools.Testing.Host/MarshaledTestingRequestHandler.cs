using System.Text.Json;
using DevTools.Execution.Abstractions;
using DevTools.Hosting;
using DevTools.Ipc;
using DevTools.Testing.Transport;

namespace DevTools.Testing.Host;

/// <summary>Runs <c>testing/run</c> on the host context while leaving control messages on the pipe thread.</summary>
public sealed class MarshaledTestingRequestHandler : IBridgeRequestHandler, IBridgeNotificationPublisher
{
    private readonly TestingRequestHandler _inner;
    private readonly IHostContextExecutor _hostContext;

    public MarshaledTestingRequestHandler(
        TestingProviderRegistry registry,
        IHostAppInfo hostInfo,
        IHostContextExecutor hostContext)
    {
        if (registry is null) throw new ArgumentNullException(nameof(registry));
        if (hostInfo is null) throw new ArgumentNullException(nameof(hostInfo));
        _inner = new TestingRequestHandler(registry, hostInfo.Host.ToString(), hostInfo.VersionNumber);
        _hostContext = hostContext ?? throw new ArgumentNullException(nameof(hostContext));
    }

    public IReadOnlyCollection<string> SupportedMethods => _inner.SupportedMethods;

    public Action<string, JsonElement?>? NotificationSender
    {
        get => _inner.NotificationSender;
        set => _inner.NotificationSender = value;
    }

    public Task<BridgeMessage> HandleAsync(string requestId, string method, JsonElement? @params,
        CancellationToken ct = default) =>
        string.Equals(method, TestingProtocol.Run, StringComparison.OrdinalIgnoreCase)
            ? HandleRunAsync(requestId, method, @params, ct)
            : _inner.HandleAsync(requestId, method, @params, ct);

    private async Task<BridgeMessage> HandleRunAsync(string requestId, string method, JsonElement? @params,
        CancellationToken ct)
    {
        ExecutionGuardContext.Mode = ExecutionGuardMode.Suppress;
        // Do not pass the pipe-disconnect token into ExecuteAsync. Cancelling the
        // dispatcher Task while the test is frozen at a breakpoint leaves idle
        // work running with a disposed CTS and parks later testing/run forever.
        return await _hostContext.ExecuteAsync(
                () => _inner.HandleAsync(requestId, method, @params, ct).GetAwaiter().GetResult())
            .ConfigureAwait(false);
    }
}
