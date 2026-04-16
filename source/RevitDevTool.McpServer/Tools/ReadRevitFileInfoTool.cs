using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RevitDevTool.McpServer.RevitFileInfo;

namespace RevitDevTool.McpServer.Tools;

public sealed class ReadRevitFileInfoTool : McpServerTool
{
    private static readonly HashSet<string> ValidExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".rvt", ".rfa", ".rft", ".rte" };

    public override Tool ProtocolTool { get; } = new()
    {
        Name = "read_revit_file_info",
        Description = "Read metadata directly from a Revit file without launching Revit. Useful for preflight checks before `launch_revit` or `open_revit_model`.",
        InputSchema = JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                filePath = new { type = "string", description = "Full path to the Revit file (.rvt, .rfa, .rft, .rte)" }
            },
            required = new[] { "filePath" }
        })
    };

    public override IReadOnlyList<object> Metadata => [];

    public override ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        string? filePath = null;
        if (request.Params?.Arguments?.TryGetValue("filePath", out var filePathElement) == true)
            filePath = filePathElement.GetString();

        if (string.IsNullOrWhiteSpace(filePath))
            return ValueTask.FromResult(ToolHelpers.ErrorResult("filePath is required."));

        if (!File.Exists(filePath))
            return ValueTask.FromResult(ToolHelpers.ErrorResult($"File not found: {filePath}"));

        var ext = Path.GetExtension(filePath);
        if (!ValidExtensions.Contains(ext))
            return ValueTask.FromResult(ToolHelpers.ErrorResult($"Invalid file extension '{ext}'. Expected: .rvt, .rfa, .rft, .rte"));

        try
        {
            var info = ReadFileInfo(filePath);
            return ValueTask.FromResult(new CallToolResult
            {
                Content = [new TextContentBlock { Text = JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true }) }]
            });
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult(ToolHelpers.ErrorResult($"Failed to read file: {ex.Message}"));
        }
    }

    private static object ReadFileInfo(string filePath)
    {
        var basicInfo = BasicFileInfoReader.Read(filePath);
        var transmissionData = TransmissionDataReader.Read(filePath);
        var worksets = WorksetParser.TryParse(filePath);

        return new
        {
            filePath,
            fileName = Path.GetFileName(filePath),
            basicInfo,
            transmissionData,
            worksets
        };
    }
}
