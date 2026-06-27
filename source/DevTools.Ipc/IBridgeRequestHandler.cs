using System.Text.Json;

namespace DevTools.Ipc;

public interface IBridgeRequestHandler
{
    IReadOnlyCollection<string> SupportedMethods { get; }
    Task<BridgeMessage> HandleAsync(string requestId, string method, JsonElement? @params, CancellationToken ct = default);
}
