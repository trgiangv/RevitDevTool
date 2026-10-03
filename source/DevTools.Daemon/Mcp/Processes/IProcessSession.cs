using System.Text.Json;
using DevTools.Ipc;
using ModelContextProtocol.Protocol;

namespace DevTools.Daemon.Mcp.Processes;

public interface IProcessSession : IAsyncDisposable
{
    bool IsConnected { get; }
    string PipeName { get; }
    InstanceInfo Info { get; }
    int ProcessId { get; }

    Task<Result> CallToolPassthroughAsync(CallToolRequestParams parameters, CancellationToken ct = default);

    Task<ReadResourceResult> ReadResourceAsync(string uri, CancellationToken ct = default);

    Task<ReadResourceResult> ReadResourceAsync(
        string uriTemplate,
        IDictionary<string, JsonElement> arguments,
        CancellationToken ct = default);
}
