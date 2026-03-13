using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RevitDevTool.Contracts;

namespace RevitDevTool.Server;

public sealed class RoutingMcpServerPrompt(InstanceManager instanceManager, Prompt protocolPrompt) : McpServerPrompt
{
    public override Prompt ProtocolPrompt { get; } = protocolPrompt;

    public override IReadOnlyList<object> Metadata => [];

    public override async ValueTask<GetPromptResult> GetAsync(
        RequestContext<GetPromptRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        var client = instanceManager.GetDefault()
                     ?? throw new InvalidOperationException("Multiple Revit instances. Specify revitInstanceId.");

        var callParams = JsonSerializer.SerializeToElement(new
        {
            name = ProtocolPrompt.Name,
            arguments = request.Params?.Arguments
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
