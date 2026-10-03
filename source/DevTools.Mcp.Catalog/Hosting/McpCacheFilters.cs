using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Hosting;

/// <summary>SDK list/read cache hints for the catalog pipe.</summary>
internal static class McpCacheFilters
{
    public static readonly TimeSpan ToolsAndTemplatesListTtl = TimeSpan.FromMilliseconds(60_000);

    public static void Attach(McpServerOptions options)
    {
        options.Filters.Request.ListToolsFilters.Add(StampListTools);
        options.Filters.Request.ListResourceTemplatesFilters.Add(StampResourceTemplates);
        options.Filters.Request.ListResourcesFilters.Add(StampListResources);
        options.Filters.Request.ReadResourceFilters.Add(StampReadResource);
    }

    private static McpRequestFilter<ListToolsRequestParams, ListToolsResult> StampListTools =>
        next => async (request, cancellationToken) =>
        {
            var result = await next(request, cancellationToken).ConfigureAwait(false);
            result.TimeToLive = ToolsAndTemplatesListTtl;
            return result;
        };

    private static McpRequestFilter<ListResourceTemplatesRequestParams, ListResourceTemplatesResult> StampResourceTemplates =>
        next => async (request, cancellationToken) =>
        {
            var result = await next(request, cancellationToken).ConfigureAwait(false);
            result.TimeToLive = ToolsAndTemplatesListTtl;
            return result;
        };

    private static McpRequestFilter<ListResourcesRequestParams, ListResourcesResult> StampListResources =>
        next => async (request, cancellationToken) =>
        {
            var result = await next(request, cancellationToken).ConfigureAwait(false);
            result.TimeToLive = TimeSpan.Zero;
            result.CacheScope = CacheScope.Private;
            return result;
        };

    private static McpRequestFilter<ReadResourceRequestParams, ReadResourceResult> StampReadResource =>
        next => async (request, cancellationToken) =>
        {
            var result = await next(request, cancellationToken).ConfigureAwait(false);
            result.TimeToLive = TimeSpan.Zero;
            return result;
        };
}
