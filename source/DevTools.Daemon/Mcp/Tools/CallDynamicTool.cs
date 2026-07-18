using System.Text.Json;
using DevTools.Mcp.Routing.Catalog;
using DevTools.Mcp.Schema;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Daemon.Mcp.Tools;

public sealed class CallDynamicTool(HostSessionManager instanceManager, DynamicToolCatalog catalog) : McpServerTool
{
    public override Tool ProtocolTool { get; } = new()
    {
        Name = "call_dynamic_tool",
        Description =
            "Call a tool currently registered by a connected host instance. " +
            "Specify hostInstanceId when multiple instances provide the same tool.",
        InputSchema = McpSchemaBuilder.Object(
        [
            McpSchemaBuilder.String(IpcPropertyNames.Name, "Registered dynamic tool name."),
            McpSchemaBuilder.Integer(McpPropertyNames.HostInstanceId, "Target host process ID."),
            McpSchemaBuilder.ObjectProp(IpcPropertyNames.Arguments, "Arguments passed to the dynamic tool.")
        ],
        required: [IpcPropertyNames.Name])
    };

    public override IReadOnlyList<object> Metadata => [];

    public override async ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        var args = request.Params.Arguments;
        var name = ReadString(args, IpcPropertyNames.Name);
        if (string.IsNullOrWhiteSpace(name))
            return ToolHelpers.ErrorResult("Dynamic tool name is required.");

        var hostInstanceId = ReadInt32(args, McpPropertyNames.HostInstanceId);
        var resolution = catalog.Resolve(name, hostInstanceId);

        switch (resolution.State)
        {
            case DynamicToolResolutionState.NotFound:
                return ToolHelpers.ErrorResult(hostInstanceId is null
                    ? $"Dynamic tool '{name}' is not registered by any connected instance."
                    : $"Dynamic tool '{name}' is not registered by host instance {hostInstanceId}.");
            case DynamicToolResolutionState.Ambiguous:
                return ToolHelpers.ErrorResult(
                    $"Dynamic tool '{name}' is available on multiple instances. Specify hostInstanceId: " +
                    string.Join(", ", resolution.Candidates.Select(FormatInstance)));
            case DynamicToolResolutionState.Found:
                break;
            default:
                throw new ArgumentOutOfRangeException($"Unexpected resolution state: {resolution.State}");
        }


        var registration = resolution.Registration!;
        var client = instanceManager.GetByProcessId(registration.Instance.ProcessId);
        if (client is null || !client.IsConnected)
            return ToolHelpers.ErrorResult($"Host instance {registration.Instance.ProcessId} is no longer connected.");

        Dictionary<string, JsonElement>? arguments = null;
        if (args?.TryGetValue(IpcPropertyNames.Arguments, out var toolArgs) == true)
            arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(toolArgs.GetRawText());

        var callParams = JsonSerializer.SerializeToElement(new McpToolsCallParams
        {
            Name = registration.Tool.Name,
            Arguments = arguments
        });

        var response = await client.RequestAsync(
                McpBridgeMethods.ToolsCall,
                callParams,
                cancellationToken)
            .ConfigureAwait(false);

        if (response.IsError)
            return ToolHelpers.ErrorResult(response.ErrorMessage ?? "Dynamic tool call failed.");

        return response.Result is { } result
            ? JsonSerializer.Deserialize<CallToolResult>(result.GetRawText())
              ?? ToolHelpers.ErrorResult("Dynamic tool returned an empty result.")
            : ToolHelpers.ErrorResult("Dynamic tool returned no result.");
    }

    private static string FormatInstance(DynamicToolCatalogEntry item) =>
        $"PID {item.Instance.ProcessId} ({item.Instance.HostApp ?? "unknown"} {item.Instance.VersionNumber})";

    private static string? ReadString(IDictionary<string, JsonElement>? args, string key) =>
        args is not null && args.TryGetValue(key, out var value) ? value.GetString() : null;

    private static int? ReadInt32(IDictionary<string, JsonElement>? args, string key) =>
        args is not null && args.TryGetValue(key, out var value) && value.TryGetInt32(out var result) ? result : null;
}
