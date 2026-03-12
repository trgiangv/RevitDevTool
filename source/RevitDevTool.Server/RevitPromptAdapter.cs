using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace RevitDevTool.Server;

public static class RevitPromptAdapter
{
    public static McpServerPrompt ToMcpServerPrompt(Prompt prompt, string promptId, RevitBridgeClient bridgeClient)
    {
        return new BridgedMcpServerPrompt(prompt, promptId, bridgeClient);
    }
}

internal sealed class BridgedMcpServerPrompt : McpServerPrompt
{
    private readonly string _promptId;
    private readonly RevitBridgeClient _bridgeClient;

    public BridgedMcpServerPrompt(Prompt prompt, string promptId, RevitBridgeClient bridgeClient)
    {
        _promptId = promptId;
        _bridgeClient = bridgeClient;
        ProtocolPrompt = prompt;
    }

    public override Prompt ProtocolPrompt { get; }

    public override IReadOnlyList<object> Metadata => [];

    public override ValueTask<GetPromptResult> GetAsync(
        RequestContext<GetPromptRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        return new ValueTask<GetPromptResult>(_bridgeClient.GetPromptAsync(
            _promptId,
            ProtocolPrompt.Name,
            (IReadOnlyDictionary<string, JsonElement>?)request.Params?.Arguments,
            cancellationToken));
    }
}
