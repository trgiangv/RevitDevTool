using System.Text.Json;
using DevTools.Daemon.Mcp.Contracts;
using DevTools.Daemon.Mcp.Processes;
using DevTools.Mcp.Core.Protocol;
using ModelContextProtocol.Extensions.Tasks;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Daemon.Mcp;

/// <summary>
/// Selects MCP task execution mode for Daemon tools.
/// Only <see cref="McpTaskExecutionMode.Synchronous"/> and
/// <see cref="McpTaskExecutionMode.Optional"/> are returned.
/// <see cref="McpTaskExecutionMode.Required"/> is unused: clients in use do not
/// advertise <c>io.modelcontextprotocol/tasks</c>, so Required fails the call
/// with <c>-32021</c> before the tool runs.
/// <c>launch_host</c> and a single <c>invoke_dynamic</c> catalog-tool call are Optional.
/// Resource reads, <c>reads</c> batches, and other infrastructure tools are synchronous.
/// </summary>
public static class TaskSelection
{
    public static McpTaskExecutionMode Select(RequestContext<CallToolRequestParams> request)
    {
        var name = request.Params.Name;

        // Required once clients advertise io.modelcontextprotocol/tasks:
        // launch_host, execute_csharp_code, execute_python_code, open_document.
        if (name is McpSpecKeys.Tool.LaunchHost)
            return McpTaskExecutionMode.Optional;

        if (name is not McpSpecKeys.Tool.Invoke)
            return McpTaskExecutionMode.Synchronous;

        // ReSharper disable once UnusedVariable
        return TryGetCatalogToolTarget(request.Params, out var target)
            ? McpTaskExecutionMode.Optional
            : McpTaskExecutionMode.Synchronous;
    }

    private static bool TryGetCatalogToolTarget(CallToolRequestParams? parameters, out string target)
    {
        target = string.Empty;
        if (parameters?.Arguments is not { Count: > 0 } arguments)
            return false;

        if (arguments.TryGetValue(InvokeValidator.Argument.Reads, out var reads)
            && reads.ValueKind is JsonValueKind.Array
            && reads.GetArrayLength() > 0)
            return false;

        if (!arguments.TryGetValue(InvokeValidator.Argument.Id, out var idNode))
            return false;

        var id = idNode.GetString();
        if (string.IsNullOrWhiteSpace(id))
            return false;

        if (!CatalogId.TryDecode(id, out var locator) || locator is null)
            return false;

        if (locator.Kind is not CatalogType.Tool)
            return false;

        target = locator.Target;
        return true;
    }
}
