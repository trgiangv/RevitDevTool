using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol;

namespace DevTools.Daemon.Mcp.Contracts;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
[JsonSerializable(typeof(ResourceReadRequest))]
[JsonSerializable(typeof(ResourceReadRequest[]))]
[JsonSerializable(typeof(InvokeState))]
[JsonSerializable(typeof(SearchResponse))]
[JsonSerializable(typeof(SearchItem))]
[JsonSerializable(typeof(SearchItem[]))]
[JsonSerializable(typeof(InvokeResponse))]
[JsonSerializable(typeof(InvocationError))]
[JsonSerializable(typeof(ResourceReadResult))]
[JsonSerializable(typeof(ResourceReadResult[]))]
[JsonSerializable(typeof(LaunchHostResult))]
[JsonSerializable(typeof(ListInstancesResult))]
[JsonSerializable(typeof(ConnectedInstance))]
[JsonSerializable(typeof(ConnectedInstance[]))]
[JsonSerializable(typeof(DiscoveredPipe))]
[JsonSerializable(typeof(DiscoveredPipe[]))]
internal sealed partial class McpServerJsonContext : JsonSerializerContext;

public static class McpToolJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(McpJsonUtilities.DefaultOptions);
        options.TypeInfoResolverChain.Insert(0, McpServerJsonContext.Default);
        return options;
    }
}
