using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using DevTool.McpParser.Models;

namespace DevTool.McpServer;

public sealed class RoutingMcpServerPrompt(InstanceManager instanceManager, Prompt protocolPrompt) : McpServerPrompt
{
    public override Prompt ProtocolPrompt { get; } = protocolPrompt;

    public override IReadOnlyList<object> Metadata => [];

    public override async ValueTask<GetPromptResult> GetAsync(
        RequestContext<GetPromptRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        var args = request.Params?.Arguments != null
            ? new Dictionary<string, JsonElement>(request.Params.Arguments)
            : new Dictionary<string, JsonElement>();

        var client = ToolHelpers.ResolveClient(instanceManager, args, out var cleanedArgs)
                     ?? throw new InvalidOperationException(ToolHelpers.FormatInstanceListing(instanceManager));

        var callParams = JsonSerializer.SerializeToElement(new
        {
            name = ProtocolPrompt.Name,
            arguments = cleanedArgs.Count > 0 ? cleanedArgs : null
        });

        var response = await client.RequestAsync(BridgeMethods.PromptsGet, callParams, cancellationToken)
            .ConfigureAwait(false);

        if (response.IsError)
            throw new InvalidOperationException(response.ErrorMessage ?? "Prompt call failed.");

        return response.Result is { } result
            ? JsonSerializer.Deserialize<GetPromptResult>(result.GetRawText()) ?? throw new InvalidOperationException("Empty prompt result.")
            : throw new InvalidOperationException("No result returned.");
    }
}
