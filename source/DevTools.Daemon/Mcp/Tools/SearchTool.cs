using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using DevTools.Daemon.Mcp.Contracts;
using DevTools.Daemon.Mcp.Processes;
using DevTools.Daemon.Mcp.Search;
using DevTools.Mcp.Core.Protocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Daemon.Mcp.Tools;

/// <summary>Fixed external tool that searches connected process catalogs without opening a pipe.</summary>
public sealed class SearchTool(IProcessSessions sessions)
{
    public const int DefaultLimit = 12;
    public const int MaximumLimit = 32;

    private const int MaximumArgsHintCount = 8;
    private const int HasMoreProbeExtraCount = 1;
    private const string Summary = "summary";
    private const string Schema = "schema";

    private static readonly Regex TemplateParameterRegex = new(@"\{([^}]+)\}", RegexOptions.CultureInvariant);

    public static McpServerTool Create(IProcessSessions sessions) => McpServerTool.Create(
        new SearchTool(sessions).Search,
        new McpServerToolCreateOptions
        {
            Name = "search_dynamic",
            Description = $"Search tools, resources, and resource templates on connected processes. Pass each item id to {McpSpecKeys.Tool.Invoke}. Use detail=schema only when the input schema is required.",
            ReadOnly = true,
            Destructive = false,
            OpenWorld = false,
        });

    [Description("Search tools, resources, and resource templates on connected processes.")]
    private CallToolResult Search(
        string? query = null,
        int? processId = null,
        string[]? kinds = null,
        int? limit = null,
        string? detail = null)
    {
        if (limit is < 1 or > MaximumLimit)
            return ToolResults.Error("validation_error", $"limit must be between 1 and {MaximumLimit}.");
        if (!TryParseDetail(detail, out var includeSchema))
            return ToolResults.Error("validation_error", $"detail must be {Summary} or {Schema}.");
        if (!TryParseKinds(kinds, out var parsedKinds, out var kindError))
            return ToolResults.Error("validation_error", kindError!);

        var requestedLimit = limit ?? DefaultLimit;
        var matches = sessions.Catalog.Search(
            query,
            parsedKinds,
            processId,
            limit: requestedLimit + HasMoreProbeExtraCount);
        var hasMore = matches.Count > requestedLimit;
        var items = matches.Take(requestedLimit).Select(match => ToItem(match, includeSchema)).ToArray();
        var response = new SearchResponse(
            items.Length,
            hasMore,
            items,
            matches.Count == 0 ? sessions.Catalog.AvailableNames(processId) : null);
        return ToolResults.Result(response, McpServerJsonContext.Default.SearchResponse, structured: true);
    }

    private static SearchItem ToItem(DevTools.Daemon.Mcp.Search.Match match, bool includeSchema)
    {
        var item = match.Item;
        var schema = item.Tool?.InputSchema;
        var templateArgs = item.Kind is CatalogType.ResourceTemplate
            ? TemplateArgs(item.ResourceTemplate)
            : null;
        var id = new CatalogId(item.ProcessId, item.Kind, item.Target, CatalogId.ContentHashFor(item)).Encode();

        return new SearchItem(
            id,
            item.Kind,
            item.Target,
            item.Description,
            item.ProcessId,
            item.Instance.HostApp,
            item.Instance.VersionNumber,
            SchemaNames(schema, McpSpecKeys.JsonSchema.Required, int.MaxValue) ?? templateArgs,
            SchemaNames(schema, McpSpecKeys.JsonSchema.Properties, MaximumArgsHintCount) ?? templateArgs,
            includeSchema ? schema : null,
            item.Resource?.MimeType ?? item.ResourceTemplate?.MimeType);
    }

    private static bool TryParseDetail(string? detail, out bool includeSchema)
    {
        includeSchema = string.Equals(detail, Schema, StringComparison.OrdinalIgnoreCase);
        return string.IsNullOrWhiteSpace(detail)
            || includeSchema
            || string.Equals(detail, Summary, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseKinds(string[]? kinds, out IReadOnlyCollection<CatalogType>? result, out string? error)
    {
        result = null;
        error = null;
        if (kinds is null || kinds.Length == 0)
            return true;

        var parsed = new List<CatalogType>();
        foreach (var kind in kinds)
        {
            if (!CatalogTypeCodec.TryParse(kind, out var value))
            {
                error = "kinds must contain only tool, resource, or resource_template.";
                return false;
            }

            parsed.Add(value);
        }

        result = parsed;
        return true;
    }

    private static string[]? TemplateArgs(ResourceTemplate? template)
    {
        if (template is null)
            return null;

        var matches = TemplateParameterRegex.Matches(template.UriTemplate);
        if (matches.Count == 0)
            return null;

        var names = new List<string>(Math.Min(matches.Count, MaximumArgsHintCount));
        for (var i = 0; i < matches.Count && names.Count < MaximumArgsHintCount; i++)
            names.Add(matches[i].Groups[1].Value);

        return names.Count == 0 ? null : names.ToArray();
    }

    private static string[]? SchemaNames(JsonElement? schema, string property, int maximum)
    {
        if (schema is not { ValueKind: JsonValueKind.Object } root || !root.TryGetProperty(property, out var values))
            return null;

        var names = property == McpSpecKeys.JsonSchema.Properties && values.ValueKind == JsonValueKind.Object
            ? values.EnumerateObject().Select(value => value.Name).Take(maximum).ToArray()
            : values.ValueKind == JsonValueKind.Array
                ? values.EnumerateArray()
                    .Where(value => value.ValueKind == JsonValueKind.String)
                    .Select(value => value.GetString()!)
                    .Take(maximum)
                    .ToArray()
                : [];

        return names.Length == 0 ? null : names;
    }
}
