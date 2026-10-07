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
                Name = "list_processes",
                Description =
                    "List connected and discovered host processes. " +
                    "Returns hostApp, processId, and version for each process.",
                ReadOnly = true,
                Destructive = false,
                OpenWorld = false,
            });
    }

    [Description("List connected and discovered host processes.")]
    public CallToolResult List()
    {
        var connected = sessions.Catalog.List();
        var discoveredPipes = pipeScanner.Discover();

        var result = new ListInstancesResult(
            connected.Select(e => new ConnectedInstance(
                (HostAppParser.ParseHostApp(e.Instance.HostApp)
                    ?? HostAppParser.FromPipeName(e.PipeName))?.ToString(),
                e.Instance.ProcessId,
                e.Instance.VersionNumber)).ToArray(),
            discoveredPipes
                .Select(p => new DiscoveredPipe(
                    p,
                    HostAppParser.FromPipeName(p)?.ToString()))
                .ToArray(),
            connected.Count,
            discoveredPipes.Count);

        return ToolResults.Result(result, McpServerJsonContext.Default.ListInstancesResult, structured: true);
    }
}
