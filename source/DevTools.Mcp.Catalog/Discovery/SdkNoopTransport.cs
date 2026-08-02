using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Catalog.Discovery;

/// <summary>Disconnected transport for SDK <see cref="RequestContext{TParams}"/> shims at the toolset boundary.</summary>
internal sealed class SdkNoopTransport : TransportBase
{
    internal SdkNoopTransport() : base("SdkNoop", NullLoggerFactory.Instance)
    {
    }

    public override Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
