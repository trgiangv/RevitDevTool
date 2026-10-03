using System.ComponentModel;
using DevTools.Daemon.Mcp.Contracts;
using DevTools.Daemon.Mcp.Processes;
using DevTools.Daemon.Mcp.Utils;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Daemon.Mcp.Tools;

public sealed class ListProcessesTool(IProcessSessions sessions, IMcpPipeScanner pipeScanner)
{
    public static McpServerTool Create(IProcessSessions sessions, IMcpPipeScanner pipeScanner)
    {
        var handler = new ListProcessesTool(sessions, pipeScanner);
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
            });
    }

    [Description("List connected and discovered host instances.")]
    public CallToolResult List()
    {
        var connected = sessions.Catalog.List();
        var discoveredPipes = pipeScanner.Discover();

        var result = new ListInstancesResult(
            connected.Select(e => new ConnectedInstance(
                (HostAppParsing.ParseHostApp(e.Instance.HostApp)
                    ?? HostAppParsing.FromPipeName(e.PipeName))?.ToString(),
                e.Instance.ProcessId,
                e.Instance.VersionNumber)).ToArray(),
            discoveredPipes
                .Select(p => new DiscoveredPipe(
                    p,
                    HostAppParsing.FromPipeName(p)?.ToString()))
                .ToArray(),
            connected.Count,
            discoveredPipes.Count);

        return ToolResults.Result(result, McpServerJsonContext.Default.ListInstancesResult, structured: true);
    }
}
