using System.Text.Json.Nodes;
using System.Text.Json;
using DevTools.Mcp.Routing.Broker;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Routing.Native;

public sealed class NativeHostToolProxy(IHostMcpSession session, Tool original) : McpServerTool
{
    public override Tool ProtocolTool { get; } = new()
    {
        Name = $"h{session.Instance.ProcessId}__{original.Name}",
        Description = original.Description,
        InputSchema = original.InputSchema,
        Annotations = original.Annotations,
        Meta = NativeHostMetadata.Create(session.Instance, original.Name)
    };

    public override IReadOnlyList<object> Metadata => [];

    public override ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken = default) =>
        new(session.CallToolAsync(
            original.Name,
            request.Params.Arguments is null ? null : BrokerArgumentConverter.ToObjects(JsonSerializer.SerializeToElement(request.Params.Arguments)),
            cancellationToken));
}

internal static class NativeHostMetadata
{
    public static JsonObject Create(HostInstanceDescriptor host, string original) => new()
    {
        ["devtools/original"] = original,
        ["devtools/hostId"] = host.ProcessId,
        ["devtools/hostApp"] = host.HostApp,
        ["devtools/hostVersion"] = host.VersionNumber
    };
}
