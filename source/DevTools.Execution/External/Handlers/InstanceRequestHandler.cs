using System.Text.Json;
using DevTools.Logging;
namespace DevTools.Execution.External.Handlers;

public sealed class InstanceRequestHandler(IHostAppInfo hostInfo) : IBridgeRequestHandler
{
    public IReadOnlyCollection<string> SupportedMethods { get; } =
        [IpcBridgeMethods.InstanceInfo];

    public Task<BridgeMessage> HandleAsync(
        string requestId,
        string method,
        JsonElement? @params,
        CancellationToken ct = default)
    {
        if (!string.Equals(method, IpcBridgeMethods.InstanceInfo, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(BridgeMessage.Error(requestId, $"Unknown method: {method}"));

        return Task.FromResult(HandleInstanceInfo(requestId));
    }

    public BridgeMessage HandleInstanceInfo(string id)
    {
        var json = JsonSerializer.SerializeToElement(BuildInstanceInfo());
        return BridgeMessage.Response(id, json);
    }

    private InstanceInfo BuildInstanceInfo() => new()
    {
        HostApp = hostInfo.Host.ToString(),
        ProcessId = Environment.ProcessId,
        VersionNumber = hostInfo.VersionNumber,
    };
}
