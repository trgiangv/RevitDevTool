using System.Text.Json.Serialization;
using DevTools.Ipc;
using ModelContextProtocol.Protocol;

namespace DevTools.Daemon.Mcp.Processes;

[JsonConverter(typeof(JsonStringEnumConverter<CatalogType>))]
public enum CatalogType
{
    [JsonStringEnumMemberName("tool")]
    Tool,

    [JsonStringEnumMemberName("resource")]
    Resource,

    [JsonStringEnumMemberName("resource_template")]
    ResourceTemplate,
}

internal static class CatalogTypeCodec
{
    public static bool TryParse(string? token, out CatalogType type)
    {
        switch (token?.Trim().ToLowerInvariant())
        {
            case "tool":
                type = CatalogType.Tool;
                return true;
            case "resource":
                type = CatalogType.Resource;
                return true;
            case "resource_template":
                type = CatalogType.ResourceTemplate;
                return true;
            default:
                type = default;
                return false;
        }
    }

    public static char Code(CatalogType type) => type switch
    {
        CatalogType.Tool => 't',
        CatalogType.Resource => 'r',
        CatalogType.ResourceTemplate => 'p',
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    public static bool TryFromCode(char code, out CatalogType type)
    {
        type = code switch
        {
            't' => CatalogType.Tool,
            'r' => CatalogType.Resource,
            'p' => CatalogType.ResourceTemplate,
            _ => default,
        };
        return code is 't' or 'r' or 'p';
    }
}

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed class ProcessCatalog
{
    public required int ProcessId { get; init; }
    public required InstanceInfo Instance { get; init; }
    public required string PipeName { get; init; }
    public required IReadOnlyList<Tool> Tools { get; init; }
    public required IReadOnlyList<Resource> Resources { get; init; }
    public required IReadOnlyList<ResourceTemplate> ResourceTemplates { get; init; }
}

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record CatalogItem(
    CatalogType Kind,
    string Target,
    string? Description,
    int ProcessId,
    InstanceInfo Instance,
    Tool? Tool = null,
    Resource? Resource = null,
    ResourceTemplate? ResourceTemplate = null);
