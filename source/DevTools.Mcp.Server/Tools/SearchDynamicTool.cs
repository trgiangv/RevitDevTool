using System.ComponentModel;
using DevTools.Mcp.Core;
using DevTools.Mcp.Server.Contracts;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Server.Tools;

/// <summary>Fixed external tool that searches the local host catalog without opening a host pipe.</summary>
/// <remarks>
/// Primary tool that triggered the SDK 2.0 structured-output workaround: return type embeds
/// <see cref="SearchCapabilityItem.InputSchema"/> (<see cref="JsonElement"/>), which breaks auto
/// <c>outputSchema</c> on <c>tools/list</c>. See <see cref="DynamicToolCallResults"/> for details.
/// TODO(sdk-2.0-clients): adopt <c>UseStructuredContent</c> + explicit <c>OutputSchema</c> once clients accept it.
/// </remarks>
public sealed class SearchDynamicTool(IHostBroker broker)
{
    public static McpServerTool Create(IHostBroker broker) => McpServerTool.Create(
        new SearchDynamicTool(broker).Search,
        new McpServerToolCreateOptions
        {
            Name = "search_dynamic",
            Description = "Search local host capabilities. Return capabilityId values and invoke them directly; use detail=schema only when needed.",
            ReadOnly = true,
            Destructive = false,
            OpenWorld = false,
            // Intentionally no UseStructuredContent — see DynamicToolCallResults.
        });

    [Description("Search local catalog capabilities.")]
    public CallToolResult Search(
        string? query = null,
        int? hostInstanceId = null,
        string[]? kinds = null,
        int? limit = null,
        string? detail = null)
    {
        if (limit is < 1 or > SearchDynamicLimits.MaximumLimit)
            return DynamicToolCallResults.Error(
                "validation_error",
                $"limit must be between 1 and {SearchDynamicLimits.MaximumLimit}.");
        if (!SearchDynamicDetailModes.TryParse(detail, out var includeSchema))
            return DynamicToolCallResults.Error(
                "validation_error",
                $"detail must be {SearchDynamicDetailModes.Summary} or {SearchDynamicDetailModes.Schema}.");
        if (!SearchDynamicWireKinds.TryParse(kinds, out var parsedKinds, out var kindError))
            return DynamicToolCallResults.Error("validation_error", kindError!);

        var requestedLimit = limit ?? SearchDynamicLimits.DefaultLimit;
        var matches = broker.Catalog.Search(
            query,
            parsedKinds,
            hostInstanceId: hostInstanceId,
            limit: requestedLimit + SearchDynamicLimits.HasMoreProbeExtraCount);
        var hasMore = matches.Count > requestedLimit;
        var items = matches.Take(requestedLimit).Select(hit => ToItem(hit, includeSchema)).ToArray();
        var response = new SearchCapabilitiesResponse(items.Length, hasMore, items);
        return DynamicToolCallResults.Result(response, structured: response);
    }

    private SearchCapabilityItem ToItem(HostCatalogHit hit, bool includeSchema)
    {
        var entry = broker.Catalog.List().First(entry => entry.Key.Equals(hit.Key));
        var schema = hit.Tool?.InputSchema;
        var templateArgs = hit.Kind is HostCatalogKind.ResourceTemplate
            ? SearchCapabilitySchemaHints.ExtractTemplateArgsHint(hit.ResourceTemplate)
            : null;
        return new(
            new DynamicCapabilityId(
                hit.Key.MachineId,
                hit.Key.ProcessId,
                hit.Kind,
                hit.Target,
                DynamicCapabilityId.CatalogVersionFor(entry),
                DynamicCapabilityId.FingerprintFor(hit)).Encode(),
            SearchDynamicWireKinds.ToWireKind(hit.Kind),
            hit.Target,
            hit.Description,
            hit.Key.MachineId,
            hit.Key.ProcessId,
            hit.Instance.HostApp,
            hit.Instance.VersionNumber,
            SearchCapabilitySchemaHints.ExtractRequiredArgs(schema) ?? templateArgs,
            SearchCapabilitySchemaHints.ExtractArgsHint(schema) ?? templateArgs,
            includeSchema ? schema : null,
            hit.Resource?.MimeType ?? hit.ResourceTemplate?.MimeType);
    }
}
