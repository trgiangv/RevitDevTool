using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Hosting;

/// <summary>
/// SDK tool placed directly in <see cref="McpServerPrimitiveCollection{T}"/>.
/// Built-in tools already are <see cref="McpServerTool"/> and are added as themselves.
/// .NET and Python sources use this type so the advertised schema stays the parsed descriptor
/// while invoke runs the source body.
/// </summary>
public sealed class CatalogToolAdapter : McpServerTool
{
    private readonly Func<RequestContext<CallToolRequestParams>, CancellationToken, Task<CallToolResult>> _invoke;

    private CatalogToolAdapter(
        Tool protocolTool,
        Func<RequestContext<CallToolRequestParams>, CancellationToken, Task<CallToolResult>> invoke)
    {
        ProtocolTool = protocolTool;
        _invoke = invoke;
    }

    public override Tool ProtocolTool { get; }
    public override IReadOnlyList<object> Metadata => [];

    public static McpServerTool Create(
        Tool protocolTool,
        Func<RequestContext<CallToolRequestParams>, CancellationToken, Task<CallToolResult>> invoke) =>
        new CatalogToolAdapter(protocolTool, invoke);

    public override async ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken = default) =>
        await _invoke(request, cancellationToken).ConfigureAwait(false);
}
