using System.Text.Json;
using ModelContextProtocol.Protocol;
namespace DevTools.Mcp.Core.Sessions;

public interface IHostSession : IAsyncDisposable
{
    bool IsConnected { get; }
    Task<HostToolCallOutcome> CallToolPassthroughAsync(CallToolRequestParams parameters, CancellationToken ct = default);
    Task<ReadResourceResult> ReadResourceAsync(string uri, CancellationToken ct = default);
    Task<ReadResourceResult> ReadResourceAsync(string uriTemplate, IDictionary<string, JsonElement> arguments, CancellationToken ct = default);
}
