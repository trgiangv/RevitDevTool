using System.Text.Json;
using DevTools.Execution.Providers.Python;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Execution.External.Mcp.Registry;

public sealed class PythonMcpServerPrompt(McpRegisteredPrompt registration, PythonExecutor executor) : McpServerPrompt
{
    public override Prompt ProtocolPrompt => registration.ProtocolPrompt;
    public override IReadOnlyList<object> Metadata => [];

    public override ValueTask<GetPromptResult> GetAsync(RequestContext<GetPromptRequestParams> request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyDictionary<string, JsonElement> arguments = request.Params.Arguments is { } values
            ? values.ToDictionary(pair => pair.Key, pair => pair.Value)
            : new Dictionary<string, JsonElement>();
        var resultJson = PythonMcpInvoker.InvokePrompt(executor, registration, arguments);
        var result = JsonSerializer.Deserialize<GetPromptResult>(resultJson, McpJsonUtilities.DefaultOptions)
            ?? throw new JsonException("Python MCP prompt returned an empty result.");
        return new ValueTask<GetPromptResult>(result);
    }
}
