using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RevitDevTool.McpParser.Models;

namespace RevitDevTool.McpServer.Tools;

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
            return ToolHelpers.ErrorResult("filePath is required.");

        if (!File.Exists(filePath))
            return ToolHelpers.ErrorResult($"File not found: {filePath}");

        var client = ToolHelpers.ResolveClient(instanceManager, args, out _);
        if (client is null)
            return ToolHelpers.ErrorResult(ToolHelpers.FormatInstanceListing(instanceManager));

        var callParams = JsonSerializer.SerializeToElement(new
        {
            name = "open_model",
            arguments = new { filePath }
        });

        var response = await client.RequestAsync(BridgeMethods.ToolsCall, callParams, cancellationToken)
            .ConfigureAwait(false);

        if (response.IsError)
            return ToolHelpers.ErrorResult(response.ErrorMessage ?? "Failed to open model.");

        if (response.Result is { } result)
            return JsonSerializer.Deserialize<CallToolResult>(result.GetRawText()) ?? ToolHelpers.ErrorResult("Empty result.");

        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = $"Model '{Path.GetFileName(filePath)}' opened successfully." }]
        };
    }
}
