namespace DevTools.Mcp.Server.Contracts;

/// <summary>Wire values for <c>search_dynamic.detail</c>.</summary>
public static class SearchDynamicDetailModes
{
    public const string Summary = "summary";
    public const string Schema = "schema";

    public static bool TryParse(string? detail, out bool includeSchema)
    {
        includeSchema = string.Equals(detail, Schema, StringComparison.OrdinalIgnoreCase);
        return string.IsNullOrWhiteSpace(detail)
            || includeSchema
            || string.Equals(detail, Summary, StringComparison.OrdinalIgnoreCase);
    }
}
