using DevTools.Execution.External.Testing;
using DevTools.Hosting;
using DevTools.Ipc;
using DevTools.Utilities;
using System.Text.Json;
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
        return Task.FromResult(!string.Equals(method, IpcBridgeMethods.InstanceInfo, StringComparison.OrdinalIgnoreCase)
            ? BridgeMessage.Error(requestId, IpcErrorCodes.MethodNotFound, $"Unknown method: {method}")
            : HandleInstanceInfo(requestId));
    }

    private BridgeMessage HandleInstanceInfo(string id)
    {
        var json = JsonSerializer.SerializeToElement(BuildInstanceInfo());
        return BridgeMessage.Response(id, json);
    }

    private InstanceInfo BuildInstanceInfo() => new()
    {
        HostApp = hostInfo.Host.ToString(),
        ProcessId = Environment.ProcessId,
        VersionNumber = hostInfo.VersionNumber,
        ProductVersion = AppVersion.Current,
        Protocols = new Dictionary<string, ProtocolRange>(StringComparer.Ordinal)
        {
            [PytestProtocol.Name] = PytestProtocol.Range,
        },
    };
}
