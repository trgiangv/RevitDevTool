using System.Text.Json;

namespace DevTools.Mcp.Routing.Broker;

public enum BrokerPrimitiveKind { Tool, Resource, Prompt }
public enum BrokerSearchDetail { Summary, Schema }

public sealed record BrokerSearchRequest(
    string? Query,
    IReadOnlyList<BrokerPrimitiveKind>? Kinds,
    int? HostId,
    BrokerSearchDetail Detail = BrokerSearchDetail.Schema,
    int Limit = 8);

public sealed record BrokerSearchItem(
    string Target,
    BrokerPrimitiveKind Kind,
    string Name,
    string? Description,
    int HostId,
    string HostApp,
    string HostVersion,
    JsonElement? Schema);

public sealed record BrokerSearchResponse(
    string Revision,
    IReadOnlyList<HostInstanceDescriptor> Hosts,
    IReadOnlyList<BrokerSearchItem> Items,
    bool Truncated);

public static class BrokerSearchRequestParser
{
    public static BrokerSearchRequest Parse(string? query, string[]? kinds, int? hostId, string detail, int limit)
    {
        var parsedKinds = kinds?.Select(ParseKind).Distinct().ToArray();
        var parsedDetail = detail.Equals("summary", StringComparison.OrdinalIgnoreCase)
            ? BrokerSearchDetail.Summary
            : detail.Equals("schema", StringComparison.OrdinalIgnoreCase)
                ? BrokerSearchDetail.Schema
                : throw new ArgumentException("detail must be summary or schema.", nameof(detail));
        if (limit is < 1 or > 20)
            throw new ArgumentOutOfRangeException(nameof(limit), "limit must be between 1 and 20.");
        return new BrokerSearchRequest(query, parsedKinds, hostId, parsedDetail, limit);
    }

    private static BrokerPrimitiveKind ParseKind(string value) => value.ToLowerInvariant() switch
    {
        "tool" => BrokerPrimitiveKind.Tool,
        "resource" => BrokerPrimitiveKind.Resource,
        "prompt" => BrokerPrimitiveKind.Prompt,
        _ => throw new ArgumentException("kinds must contain tool, resource, or prompt.", nameof(value))
    };
}

public static class BrokerArgumentConverter
{
    public static IReadOnlyDictionary<string, object?>? ToObjects(JsonElement? arguments)
    {
        if (arguments is not { ValueKind: JsonValueKind.Object } element)
            return null;

        return element.EnumerateObject().ToDictionary(property => property.Name, property => ToObject(property.Value));
    }

    private static object? ToObject(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.String => value.GetString(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number when value.TryGetInt64(out var integer) => integer,
        JsonValueKind.Number => value.GetDouble(),
        JsonValueKind.Array => value.EnumerateArray().Select(ToObject).ToArray(),
        JsonValueKind.Object => value.EnumerateObject().ToDictionary(property => property.Name, property => ToObject(property.Value)),
        _ => value.GetRawText()
    };
}
