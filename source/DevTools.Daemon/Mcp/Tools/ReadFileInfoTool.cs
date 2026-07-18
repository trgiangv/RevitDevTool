using System.Text.Json;
using DevTools.Daemon.Contracts;
using DevTools.Daemon.Hosts;
using DevTools.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Daemon.Mcp.Tools;

public sealed class ReadFileInfoTool(HostDriverRegistry drivers) : McpServerTool
{
    private static readonly string[] AllExtensions = [".rvt", ".rfa", ".rft", ".rte", ".dwg"];

    public override Tool ProtocolTool { get; } = new()
    {
        Name = "read_file_info",
        Description =
            "Read metadata from a CAD/BIM file offline (no host launch needed). " +
            "Revit (.rvt/.rfa/.rft/.rte): version, worksets, links, project info. " +
            "AutoCAD (.dwg): version, layers, blocks, document properties.",
        InputSchema = JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new { filePath = new { type = "string", description = "Full path to the file (.rvt, .rfa, .rft, .rte, .dwg)." } },
            required = new[] { "filePath" }
        })
    };

    public override IReadOnlyList<object> Metadata => [];

    public override async ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        string? filePath = null;
        if (request.Params.Arguments?.TryGetValue("filePath", out var filePathElement) == true)
            filePath = filePathElement.GetString();

        if (string.IsNullOrWhiteSpace(filePath))
            return ToolHelpers.ErrorResult("filePath is required.");

        if (!File.Exists(filePath))
            return ToolHelpers.ErrorResult($"File not found: {filePath}");

        var ext = Path.GetExtension(filePath);
        var driver = drivers.TryForFile(filePath);

        if (driver is null)
        {
            var hostApp = HostAppExtensions.FromExtension(ext);
            if (hostApp is not null)
                return ToolHelpers.ErrorResult($"Failed to read file: No reader for {hostApp}");

            var supported = string.Join(", ", AllExtensions);
            return ToolHelpers.ErrorResult($"Unsupported file extension '{ext}'. Supported: {supported}");
        }

        try
        {
            var info = await driver.ReadFileInfoAsync(filePath, cancellationToken).ConfigureAwait(false);

            var json = JsonSerializer.Serialize(info, info.GetType(), ToolHelpers.IndentedJsonOptions);
            return new CallToolResult
            {
                Content = [new TextContentBlock { Text = json }]
            };
        }
        catch (Exception ex)
        {
            return ToolHelpers.ErrorResult($"Failed to read file: {ex.Message}");
        }
    }
}
