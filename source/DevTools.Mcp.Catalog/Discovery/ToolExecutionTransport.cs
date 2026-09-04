using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Catalog.Discovery;

/// <summary>Non-network transport used while executing a catalog-resolved tool.</summary>
internal sealed class ToolExecutionTransport : TransportBase
{
    internal ToolExecutionTransport() : base("ToolExecution", NullLoggerFactory.Instance)
    {
    }

    public override Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
