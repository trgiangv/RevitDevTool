using DevTools.Mcp.Catalog;
using DevTools.Mcp.Catalog.Bridging;
using DevTools.Mcp.Catalog.Discovery;
using DevTools.Mcp.Core.Invocation;
using DevTools.Mcp.Core.Models;
using DevTools.Mcp.Core.Results;
using ModelContextProtocol.Protocol;

namespace DevTools.Execution.External.Mcp.Backends;

/// <summary>Invokes host-owned built-in MCP tools and resources.</summary>
public sealed class BuiltInMcpToolBackend(
    IEnumerable<IBuiltInMcpTool> tools,
    IEnumerable<IBuiltInMcpResource> resources) : IMcpPrimitiveBackend
{
    private readonly Dictionary<string, IBuiltInMcpTool> _tools =
        tools.ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IBuiltInMcpResource> _resources =
        resources.ToDictionary(item => item.UriTemplate, StringComparer.OrdinalIgnoreCase);

    public ExecutionMode SourceKind => ExecutionMode.CSharp;

    public async Task<McpResult<McpInvocationResponse>> InvokeToolAsync(
        McpRegisteredTool tool,
        CallToolRequestParams request,
        IHostContextExecutor hostContext,
        CancellationToken cancellationToken)
    {
        if (!_tools.TryGetValue(tool.Descriptor.Name, out var builtIn))
        {
            return McpResult<McpInvocationResponse>.Failure(new McpError(
                McpErrorCode.ExecutionFailed,
                $"No built-in tool registered for '{tool.Descriptor.Name}'.",
                []));
        }

        var context = RequestFactory.ToToolContext(tool.Descriptor.Name, request);
        var result = await builtIn.ServerTool.InvokeAsync(context, cancellationToken).ConfigureAwait(false);
        return McpResult<McpInvocationResponse>.Success(
            ToolsetResultSerializer.ToInvocationResponse(result, null));
    }

    public ReadResourceResult ReadResource(
        McpRegisteredResource resource,
        string uri,
        CancellationToken cancellationToken)
    {
        var template = resource.Descriptor?.Uri ?? resource.TemplateDescriptor?.UriTemplate ?? string.Empty;
        if (!_resources.TryGetValue(template, out var builtIn))
            throw new InvalidOperationException($"No built-in resource registered for '{template}'.");
        return builtIn.Read(uri);
    }

    public void ClearCaches()
    {
    }
}
