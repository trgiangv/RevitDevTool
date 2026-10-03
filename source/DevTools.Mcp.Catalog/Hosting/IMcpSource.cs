using DevTools.Execution.Abstractions;
using DevTools.Mcp.Core.Models;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Hosting;

/// <summary>One execution mode. It builds the SDK tool and resource the pipe server invokes.</summary>
public interface IMcpSource
{
    ExecutionMode SourceKind { get; }

    McpServerTool CreateTool(RegisteredTool tool, IHostContextExecutor hostContext);

    McpServerResource CreateResource(RegisteredResource resource, IHostContextExecutor hostContext);

    void ClearCaches();
}
