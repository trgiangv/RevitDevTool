using System.Text.Json;
using DevTools.Daemon.Contracts;
using DevTools.Logging;
using DevTools.Daemon.Mcp.AcadFileInfo;
using DevTools.Daemon.Mcp.RevitFileInfo;
using DevTools.Mcp.Schema;
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
        InputSchema = McpSchemaBuilder.Object(
        [
            McpSchemaBuilder.String(
                McpPropertyNames.FilePath,
                "Full path to the file (.rvt, .rfa, .rft, .rte, .dwg).")
        ],
        required: [McpPropertyNames.FilePath])
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
            FileInfoResult info = hostApp.Value switch
            {
                HostApp.Revit => ReadRevitInfo(filePath),
                _ when hostApp.Value.IsAcadFamily() => ReadDwgInfo(hostApp.Value, filePath),
                _ => throw new NotSupportedException($"No reader for {hostApp}")
            };

            var json = JsonSerializer.Serialize(info, info.GetType(), ToolHelpers.IndentedJsonOptions);
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

    private static RevitFileInfoResult ReadRevitInfo(string filePath)
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

        return new RevitFileInfoResult
        {
            HostApp = HostApp.Revit,
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),
            BasicInfo = basicInfo!,
            TransmissionData = transmissionData,
            ProjectInformation = projectInformation,
            Worksets = worksets,
            PartitionSummary = partitionSummary,
            BrowserOrganization = browserOrganization
        };
    }

    private static DwgFileInfoResult ReadDwgInfo(HostApp hostApp, string filePath)
    {
        var info = DwgFileInfoReader.Read(filePath);
        return new DwgFileInfoResult
        {
            HostApp = hostApp,
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),
            AcadVersion = info.AcadVersion,
            Title = info.Title,
            Subject = info.Subject,
            Author = info.Author,
            Keywords = info.Keywords,
            Comments = info.Comments,
            LastSavedBy = info.LastSavedBy,
            LayerCount = info.LayerCount,
            BlockCount = info.BlockCount,
            Layers = info.Layers
        };
    }
}
