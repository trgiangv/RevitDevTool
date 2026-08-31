using DevTools.Mcp.Core.Sessions;

namespace DevTools.Mcp.Server.Contracts;

/// <summary>Wire <c>kind</c> strings for <c>search_dynamic</c> items and filter parameter.</summary>
public static class SearchDynamicWireKinds
{
    private const string Tool = "tool";
    private const string Resource = "resource";
    private const string ResourceTemplate = "resource_template";

    public static bool TryParse(string[]? kinds, out IReadOnlyCollection<HostCatalogKind>? result, out string? error)
    {
        result = null;
        error = null;
        if (kinds is null || kinds.Length == 0)
            return true;

        var parsed = new List<HostCatalogKind>();
        foreach (var kind in kinds)
        {
            var value = kind.Trim().ToLowerInvariant() switch
            {
                Tool => HostCatalogKind.Tool,
                Resource => HostCatalogKind.Resource,
                ResourceTemplate => HostCatalogKind.ResourceTemplate,
                _ => (HostCatalogKind?)null
            };
            if (value is null)
            {
                error = $"kinds must contain only {Tool}, {Resource}, or {ResourceTemplate}.";
                return false;
            }

            parsed.Add(value.Value);
        }

        result = parsed;
        return true;
    }

    public static string ToWireKind(HostCatalogKind kind) => kind switch
    {
        HostCatalogKind.Tool => Tool,
        HostCatalogKind.Resource => Resource,
        HostCatalogKind.ResourceTemplate => ResourceTemplate,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}
