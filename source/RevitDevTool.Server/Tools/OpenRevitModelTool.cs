using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RevitDevTool.Contracts;

namespace RevitDevTool.Server.Tools;

public sealed class OpenRevitModelTool(InstanceManager instanceManager) : McpServerTool
{
    public override Tool ProtocolTool { get; } = new()
    {
        Name = "open_revit_model",
        Description = "Open a Revit model file (.rvt, .rfa, .rft, .rte) in a connected Revit instance. Requires at least one Revit instance to be running and connected.",
        InputSchema = JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                filePath = new { type = "string", description = "Full path to the Revit file to open" },
                revitInstanceId = new { type = "integer", description = "Target Revit process ID. Required when multiple instances are connected." }
            },
            required = new[] { "filePath" }
        })
    };

    public override IReadOnlyList<object> Metadata => [];

    public override async ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        var args = request.Params?.Arguments ?? new Dictionary<string, JsonElement>();

        string? filePath = null;
        if (args.TryGetValue("filePath", out var filePathElement))
            filePath = filePathElement.GetString();

        if (string.IsNullOrWhiteSpace(filePath))
            return ErrorResult("filePath is required.");

        if (!File.Exists(filePath))
            return ErrorResult($"File not found: {filePath}");

        var client = ResolveClient(args);
        if (client is null)
        {
            var instances = instanceManager.GetInstances();
            if (instances.Count == 0)
                return ErrorResult("No Revit instances connected. Launch Revit first with launch_revit tool.");

            var listing = string.Join(", ", instances.Select(i => $"PID {i.ProcessId} ({i.DocumentTitle ?? "no doc"})"));
            return ErrorResult($"Multiple Revit instances. Specify 'revitInstanceId': {listing}");
        }

        var callParams = JsonSerializer.SerializeToElement(new
        {
            name = "open_model",
            arguments = new { filePath }
        });

        var response = await client.RequestAsync(BridgeMethods.ToolsCall, callParams, cancellationToken)
            .ConfigureAwait(false);

        if (response.IsError)
            return ErrorResult(response.ErrorMessage ?? "Failed to open model.");

        if (response.Result is { } result)
            return JsonSerializer.Deserialize<CallToolResult>(result.GetRawText()) ?? ErrorResult("Empty result.");

        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = $"Model '{Path.GetFileName(filePath)}' opened successfully." }]
        };
    }

    private RevitBridgeClient? ResolveClient(IDictionary<string, JsonElement> args)
    {
        if (args.TryGetValue("revitInstanceId", out var pidElement))
            return instanceManager.GetByProcessId(InstanceManager.ParseProcessId(pidElement));

        return instanceManager.GetDefault();
    }

    private static CallToolResult ErrorResult(string message) =>
        new() { IsError = true, Content = [new TextContentBlock { Text = message }] };
}
