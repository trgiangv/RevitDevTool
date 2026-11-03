using DevTools.Daemon.Mcp.Tools;
using DevTools.Mcp.Core.Protocol;
using ModelContextProtocol.Extensions.Tasks;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Daemon.Mcp;

/// <summary>
/// Selects MCP task execution mode for Daemon tools.
/// Only <see cref="McpTaskExecutionMode.Synchronous"/> and
/// <see cref="McpTaskExecutionMode.Optional"/> are returned.
/// <see cref="McpTaskExecutionMode.Required"/> is unused: a client that does not
/// advertise <c>io.modelcontextprotocol/tasks</c> fails the call with
/// <c>-32021</c> before the tool runs.
/// <c>launch_host</c> and <c>code_mode</c> are Optional.
/// The other infrastructure tools are synchronous.
/// </summary>
public static class TaskSelection
{
    public static McpTaskExecutionMode Select(RequestContext<CallToolRequestParams> request)
    {
        var name = request.Params.Name;
        return name is McpSpecKeys.Tool.LaunchHost or CodeModeTool.Name
            ? McpTaskExecutionMode.Optional
            : McpTaskExecutionMode.Synchronous;
    }
}
