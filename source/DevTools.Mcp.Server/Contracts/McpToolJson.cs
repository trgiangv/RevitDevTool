using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol;

namespace DevTools.Mcp.Server.Contracts;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
[JsonSerializable(typeof(ResourceReadRequest))]
[JsonSerializable(typeof(ResourceReadRequest[]))]
[JsonSerializable(typeof(InvokeDynamicMrtrState))]
[JsonSerializable(typeof(SearchCapabilitiesResponse))]
[JsonSerializable(typeof(SearchCapabilityItem))]
[JsonSerializable(typeof(SearchCapabilityItem[]))]
[JsonSerializable(typeof(InvokeCapabilityResponse))]
[JsonSerializable(typeof(DynamicInvocationError))]
[JsonSerializable(typeof(ResourceReadResult))]
[JsonSerializable(typeof(ResourceReadResult[]))]
[JsonSerializable(typeof(LaunchHostResult))]
[JsonSerializable(typeof(ListInstancesResult))]
[JsonSerializable(typeof(ConnectedInstanceEntry))]
[JsonSerializable(typeof(ConnectedInstanceEntry[]))]
[JsonSerializable(typeof(DiscoveredPipeEntry))]
[JsonSerializable(typeof(DiscoveredPipeEntry[]))]
[JsonSerializable(typeof(DynamicCapabilityId))]
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
