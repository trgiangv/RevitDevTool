using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using DevTools.Mcp.Routing;
using DevTools.Mcp.Routing.Broker;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Daemon.Mcp.Tools;

[McpServerToolType]
public sealed class DevToolsBrokerTools(BrokerCatalogIndex catalog, IInstanceManager sessions)
{
    [McpServerTool(Name = "devtools_search")]
    [Description("Search connected hosts and runtime tools, resources, and prompts. Returns callable targets and schemas from a cached snapshot.")]
    public BrokerSearchResponse Search(
        [Description("Name, URI, description, or host text. Omit to browse.")] string? query = null,
        [Description("Optional kinds: tool, resource, prompt.")] string[]? kinds = null,
        [Description("Optional host process ID local to the daemon selected by the gateway.")] int? hostId = null,
        [Description("schema includes input schemas; summary omits them.")] string detail = "schema",
        [Description("Maximum results from 1 through 20.")] int limit = 8) =>
        catalog.Search(BrokerSearchRequestParser.Parse(query, kinds, hostId, detail, limit));

    [McpServerTool(Name = "devtools_invoke")]
    [Description("Invoke a runtime target. Use tool:<name>, resource:<uri>, or prompt:<name>. A unique target needs no hostId.")]
    public Task<CallToolResult> InvokeAsync(
        [Description("Target returned by devtools_search.")] string target,
        [Description("Optional host process ID when multiple hosts provide the target.")] int? hostId = null,
        [Description("Tool or prompt arguments. Resources ignore this value.")] JsonElement? arguments = null,
        [Description("Deadline in seconds from 1 through 900; defaults to 300.")]
        [Range(1, 900)]
        int timeoutSeconds = 300,
        CancellationToken cancellationToken = default)
    {
        if (timeoutSeconds is < 1 or > 900)
            throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), "timeoutSeconds must be between 1 and 900.");

        return catalog.InvokeAsync(
            sessions,
            BrokerPrimitiveTarget.Parse(target),
            hostId,
            arguments,
            TimeSpan.FromSeconds(timeoutSeconds),
            cancellationToken);
    }
}
