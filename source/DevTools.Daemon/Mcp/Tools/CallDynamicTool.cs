using System.Text.Json;
using DevTools.McpParser.Models;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Daemon.Mcp.Tools;

public sealed class CallDynamicTool(InstanceManager instanceManager, DynamicToolCatalog catalog) : McpServerTool
{
    public override Tool ProtocolTool { get; } = new()
    {
        Name = "call_dynamic_tool",
        Description =
            "Call a tool currently registered by a connected host instance. " +
            "Specify hostInstanceId when multiple instances provide the same tool.",
        InputSchema = JsonSerializer.SerializeToElement(new
        {
            type = JsonSchemaTypeNames.Object,
            properties = new
            {
                name = new { type = JsonSchemaTypeNames.String, description = "Registered dynamic tool name." },
                hostInstanceId = new { type = JsonSchemaTypeNames.Integer, description = "Target host process ID." },
                arguments = new { type = JsonSchemaTypeNames.Object, description = "Arguments passed to the dynamic tool." }
            },
            required = new[] { McpPropertyNames.Name }
        })
    };

    public override IReadOnlyList<object> Metadata => [];

    public override async ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        var args = request.Params.Arguments;
        var name = ReadString(args, McpPropertyNames.Name);
        if (string.IsNullOrWhiteSpace(name))
            return ToolHelpers.ErrorResult("Dynamic tool name is required.");

        var hostInstanceId = ReadInt32(args, McpPropertyNames.HostInstanceId);
        var resolution = catalog.Resolve(name, hostInstanceId);
        if (resolution.State == DynamicToolResolutionState.NotFound)
            return ToolHelpers.ErrorResult(hostInstanceId is null
                ? $"Dynamic tool '{name}' is not registered by any connected instance."
                : $"Dynamic tool '{name}' is not registered by host instance {hostInstanceId}.");

        if (resolution.State == DynamicToolResolutionState.Ambiguous)
            return ToolHelpers.ErrorResult(
                $"Dynamic tool '{name}' is available on multiple instances. Specify hostInstanceId: " +
                string.Join(", ", resolution.Candidates.Select(FormatInstance)));

        var registration = resolution.Registration!;
        var client = instanceManager.GetByProcessId(registration.Instance.ProcessId);
        if (client is null || !client.IsConnected)
            return ToolHelpers.ErrorResult($"Host instance {registration.Instance.ProcessId} is no longer connected.");

        var callParams = new Dictionary<string, object?> { [McpPropertyNames.Name] = registration.Tool.Name };
        if (args?.TryGetValue(McpPropertyNames.Arguments, out var toolArgs) == true)
            callParams[McpPropertyNames.Arguments] = toolArgs;

        var response = await client.RequestAsync(
                McpBridgeMethods.ToolsCall,
                JsonSerializer.SerializeToElement(callParams),
                cancellationToken)
            .ConfigureAwait(false);

        if (response.IsError)
            return ToolHelpers.ErrorResult(response.ErrorMessage ?? "Dynamic tool call failed.");

        return response.Result is { } result
            ? JsonSerializer.Deserialize<CallToolResult>(result.GetRawText())
              ?? ToolHelpers.ErrorResult("Dynamic tool returned an empty result.")
            : ToolHelpers.ErrorResult("Dynamic tool returned no result.");
    }

    private static string FormatInstance(DynamicToolRegistration item) =>
        $"PID {item.Instance.ProcessId} ({item.Instance.HostApp ?? "unknown"} {item.Instance.VersionNumber})";

    private static string? ReadString(IDictionary<string, JsonElement>? args, string key) =>
        args is not null && args.TryGetValue(key, out var value) ? value.GetString() : null;

    private static int? ReadInt32(IDictionary<string, JsonElement>? args, string key) =>
        args is not null && args.TryGetValue(key, out var value) && value.TryGetInt32(out var result) ? result : null;
}
