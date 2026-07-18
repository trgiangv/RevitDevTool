using System.ComponentModel;
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
        CancellationToken cancellationToken = default) =>
        catalog.InvokeAsync(sessions, BrokerPrimitiveTarget.Parse(target), hostId, arguments, cancellationToken);
}
