using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RevitDevTool.Server.RevitFileInfo;

namespace RevitDevTool.Server.Tools;

public sealed class ReadRevitFileInfoTool : McpServerTool
{
    private static readonly HashSet<string> ValidExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".rvt", ".rfa", ".rft", ".rte" };

    public override Tool ProtocolTool { get; } = new()
    {
        Name = "read_revit_file_info",
        Description = "Read basic information from a Revit file (.rvt, .rfa, .rft, .rte) without requiring Revit to be running. Returns version, author, worksharing status, and file paths.",
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
            return ValueTask.FromResult(ErrorResult("filePath is required."));

        if (!File.Exists(filePath))
            return ValueTask.FromResult(ErrorResult($"File not found: {filePath}"));

        var ext = Path.GetExtension(filePath);
        if (!ValidExtensions.Contains(ext))
            return ValueTask.FromResult(ErrorResult($"Invalid file extension '{ext}'. Expected: .rvt, .rfa, .rft, .rte"));

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
            return ValueTask.FromResult(ErrorResult($"Failed to read file: {ex.Message}"));
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

    private static CallToolResult ErrorResult(string message) =>
        new() { IsError = true, Content = [new TextContentBlock { Text = message }] };
}
