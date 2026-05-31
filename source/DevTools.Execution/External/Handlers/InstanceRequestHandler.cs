using System.Text.Json;
using DevTools.Logging;
using DevTools.McpParser.Models;
namespace DevTools.Execution.External.Handlers;

public sealed class InstanceRequestHandler(IHostAppInfo hostInfo)
{
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
