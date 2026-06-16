using System.Text.Json;
using DevTools.Logging;
using DevTools.Daemon.Mcp.AcadFileInfo;
using DevTools.McpParser.Models;
using DevTools.Daemon.Mcp.RevitFileInfo;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Daemon.Mcp.Tools;

public sealed class ReadFileInfoTool : McpServerTool
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
            type = JsonSchemaTypeNames.Object,
            properties = new
            {
                filePath = new
                {
                    type = JsonSchemaTypeNames.String,
                    description = "Full path to the file (.rvt, .rfa, .rft, .rte, .dwg)."
                }
            },
            required = new[] { McpPropertyNames.FilePath }
        })
    };

    public override IReadOnlyList<object> Metadata => [];

    public override ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        string? filePath = null;
        if (request.Params.Arguments?.TryGetValue(McpPropertyNames.FilePath, out var filePathElement) == true)
            filePath = filePathElement.GetString();

        if (string.IsNullOrWhiteSpace(filePath))
            return ValueTask.FromResult(ToolHelpers.ErrorResult("filePath is required."));

        if (!File.Exists(filePath))
            return ValueTask.FromResult(ToolHelpers.ErrorResult($"File not found: {filePath}"));

        var ext = Path.GetExtension(filePath);
        var hostApp = HostAppExtensions.FromExtension(ext);

        if (hostApp is null)
        {
            var supported = string.Join(", ", AllExtensions);
            return ValueTask.FromResult(
                ToolHelpers.ErrorResult($"Unsupported file extension '{ext}'. Supported: {supported}"));
        }

        try
        {
            object info = hostApp.Value switch
            {
                HostApp.Revit => ReadRevitInfo(filePath),
                _ when hostApp.Value.IsAcadFamily() => ReadDwgInfo(filePath),
                _ => throw new NotSupportedException($"No reader for {hostApp}")
            };

            var json = JsonSerializer.Serialize(info, ToolHelpers.IndentedJsonOptions);
            return ValueTask.FromResult(new CallToolResult
            {
                Content = [new TextContentBlock { Text = json }]
            });
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult(ToolHelpers.ErrorResult($"Failed to read file: {ex.Message}"));
        }
    }

    private static object ReadRevitInfo(string filePath)
    {
        using var file = RevitCompoundFile.Open(filePath);

        var basicInfo = BasicFileInfoReader.Read(file);
        var transmissionData = TransmissionDataReader.Read(file);
        var projectInformation = ProjectInformationReader.Read(file);

        using var ptStream = file.TryReadStream("Global", "PartitionTable");
        var ptDecompressed = ptStream is not null
            ? PartitionTableReader.Decompress(ptStream.ToArray())
            : [];

        var worksets = WorksetParser.TryParse(ptDecompressed);
        var partitionSummary = PartitionTableReader.Read(ptDecompressed);
        var browserOrganization = BrowserOrganizationReader.Read(file);

        return new
        {
            hostApp = "Revit",
            filePath,
            fileName = Path.GetFileName(filePath),
            basicInfo,
            transmissionData,
            projectInformation,
            worksets,
            partitionSummary,
            browserOrganization
        };
    }

    private static object ReadDwgInfo(string filePath)
    {
        var info = DwgFileInfoReader.Read(filePath);
        return new
        {
            hostApp = "AutoCAD",
            filePath,
            fileName = Path.GetFileName(filePath),
            info.AcadVersion,
            info.Title,
            info.Subject,
            info.Author,
            info.Keywords,
            info.Comments,
            info.LastSavedBy,
            info.LayerCount,
            info.BlockCount,
            info.Layers
        };
    }
}
