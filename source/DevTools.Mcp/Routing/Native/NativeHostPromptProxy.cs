using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.Text.Json;
using DevTools.Mcp.Routing.Broker;

namespace DevTools.Mcp.Routing.Native;

public sealed class NativeHostPromptProxy(IHostMcpSession session, Prompt original) : McpServerPrompt
{
    public override Prompt ProtocolPrompt { get; } = new()
    {
        Name = $"h{session.Instance.ProcessId}__{original.Name}",
        Title = original.Title,
        Description = original.Description,
        Arguments = original.Arguments,
        Meta = NativeHostMetadata.Create(session.Instance, original.Name)
    };

    public override IReadOnlyList<object> Metadata => [];

    public override ValueTask<GetPromptResult> GetAsync(
        RequestContext<GetPromptRequestParams> request,
        CancellationToken cancellationToken = default) =>
        new(session.GetPromptAsync(
            original.Name,
            request.Params.Arguments is null ? null : BrokerArgumentConverter.ToObjects(JsonSerializer.SerializeToElement(request.Params.Arguments)),
            cancellationToken));
}
