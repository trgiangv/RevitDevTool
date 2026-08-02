namespace DevTools.Mcp.Server.Contracts;

/// <summary>Validated bounds for <c>search_dynamic</c> query parameters.</summary>
public static class SearchDynamicLimits
{
    public const int DefaultLimit = 12;
    public const int MaximumLimit = 32;

    /// <summary>Max names in <c>argsHint</c> for tools (schema properties) and resource templates (URI parameters).</summary>
    public const int MaximumArgsHintCount = 8;

    /// <summary>Extra catalog row fetched to detect <c>hasMore</c> without a second search.</summary>
    public const int HasMoreProbeExtraCount = 1;
}
