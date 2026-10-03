using DevTools.Execution.Abstractions;
using DevTools.Mcp.Bridging;
using DevTools.Mcp.Hosting;
using DevTools.Mcp.Core.Models;
using DevTools.Mcp.Core.Utils;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Execution.External.Mcp.Backends;

/// <summary>Invokes host-owned built-in MCP tools and resources.</summary>
public sealed class BuiltInSource(
    IEnumerable<IBuiltInMcpTool> tools,
    IEnumerable<IBuiltInMcpResource> resources) : IMcpSource
{
    private readonly Dictionary<string, IBuiltInMcpTool> _tools =
        tools.ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IBuiltInMcpResource> _resources =
        resources.ToDictionary(item => item.UriTemplate, StringComparer.OrdinalIgnoreCase);

    public ExecutionMode SourceKind => ExecutionMode.CSharp;

    public McpServerTool CreateTool(RegisteredTool tool, IHostContextExecutor hostContext)
    {
        if (_tools.TryGetValue(tool.Descriptor.Name, out var builtIn))
            return builtIn.ServerTool;

        return SdkCollectionTool.Create(
            tool.Descriptor,
            (_, _) => Task.FromResult(ToolHelpers.ErrorResult($"No built-in tool registered for '{tool.Descriptor.Name}'.")));
    }

    public McpServerResource CreateResource(RegisteredResource resource, IHostContextExecutor hostContext) =>
        SdkCollectionResource.Create(resource, hostContext, (uri, cancellationToken) => ReadResourceAsync(resource, uri, cancellationToken));

    public async Task<CallToolResult> InvokeToolAsync(
        RegisteredTool tool,
        CallToolRequestParams request,
        IHostContextExecutor hostContext,
        CancellationToken cancellationToken)
    {
        if (!_tools.TryGetValue(tool.Descriptor.Name, out var builtIn))
            return ToolHelpers.ErrorResult($"No built-in tool registered for '{tool.Descriptor.Name}'.");

        var context = RequestFactory.ToToolContext(tool.Descriptor.Name, request);
        return await builtIn.ServerTool.InvokeAsync(context, cancellationToken).ConfigureAwait(false);
    }

    public Task<ReadResourceResult> ReadResourceAsync(
        RegisteredResource resource,
        string uri,
        CancellationToken cancellationToken)
    {
        var template = resource.Descriptor?.Uri ?? resource.TemplateDescriptor?.UriTemplate ?? string.Empty;
        if (!_resources.TryGetValue(template, out var builtIn))
            throw new InvalidOperationException($"No built-in resource registered for '{template}'.");
        return Task.FromResult(builtIn.Read(uri));
    }

    public void ClearCaches()
    {
    }
}
