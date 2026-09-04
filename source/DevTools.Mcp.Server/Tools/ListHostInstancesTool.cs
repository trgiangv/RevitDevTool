using System.ComponentModel;
using DevTools.Mcp.Client;
using DevTools.Mcp.Core.Sessions;
using DevTools.Mcp.Server.Contracts;
using DevTools.Mcp.Server.Utils;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Server.Tools;

/// <remarks>
/// Structured output via <see cref="DynamicToolResults"/> — same SDK 2.0 workaround as
/// <see cref="SearchDynamicTool"/> (nullable enum wire shape broke strict <c>tools/list</c> validation).
/// See 0027 / 0031 — UseStructuredContent deferred.
/// </remarks>
public sealed class ListHostInstancesTool(IHostBroker hostBroker, IMcpPipeScanner pipeScanner)
{
    public static McpServerTool Create(IHostBroker hostBroker, IMcpPipeScanner pipeScanner)
    {
        var handler = new ListHostInstancesTool(hostBroker, pipeScanner);
        return McpServerTool.Create(
            handler.List,
            new McpServerToolCreateOptions
            {
                Name = "list_host_instances",
                Description =
                    "List connected and discovered host instances. " +
                    "Returns hostApp, processId, and version for each instance.",
                ReadOnly = true,
                Destructive = false,
                OpenWorld = false,
                // Intentionally no UseStructuredContent — see DynamicToolResults.
            });
    }

    [Description("List connected and discovered host instances.")]
    public CallToolResult List()
    {
        var connected = hostBroker.Catalog.List();
        var discoveredPipes = pipeScanner.Discover();

        var result = new ListInstancesResult(
            connected.Select(e => new ConnectedInstanceEntry(
                (HostAppParsing.ParseHostApp(e.Instance.HostApp)
                    ?? HostAppParsing.FromPipeName(e.PipeName))?.ToString(),
                e.Instance.ProcessId,
                e.Instance.VersionNumber)).ToArray(),
            discoveredPipes
                .Select(p => new DiscoveredPipeEntry(
                    p,
                    HostAppParsing.FromPipeName(p)?.ToString()))
                .ToArray(),
            connected.Count,
            discoveredPipes.Count);

        return DynamicToolResults.Result(result, McpServerJsonContext.Default.ListInstancesResult, structured: true);
    }
}
