using System.ComponentModel;
using System.Text.Json;
using DevTools.FileMetadata.Core;
using DevTools.Mcp.Core.Utils;
using DevTools.Mcp.Server.Contracts;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Server.Tools;

/// <remarks>
/// Structured output via <see cref="DynamicToolResults"/> — same SDK 2.0 workaround as
/// <see cref="SearchDynamicTool"/> (open metadata shape breaks auto <c>outputSchema</c>).
/// See 0027 / 0031 — UseStructuredContent deferred.
/// </remarks>
public sealed class ReadFileInfoTool(IFileReaderCatalog catalog)
{
    public static McpServerTool Create(IFileReaderCatalog catalog)
    {
        var handler = new ReadFileInfoTool(catalog);
        return McpServerTool.Create(
            handler.Read,
            new McpServerToolCreateOptions
            {
                Name = "read_file_info",
                Description =
                    "Read metadata from a CAD/BIM file on disk (no host launch needed). " +
                    "Revit (.rvt/.rfa/.rft/.rte): version, worksets, links, project info. " +
                    "AutoCAD (.dwg): version, layers, blocks, document properties. " +
                    "Use detail=summary (default) for agent peek; detail=full for complete metadata.",
                ReadOnly = true,
                Destructive = false,
                OpenWorld = false,
                // Intentionally no UseStructuredContent — see DynamicToolResults.
            });
    }

    [Description("Read metadata from a CAD/BIM file on disk.")]
    private CallToolResult Read(
        [Description("Full path to the file (.rvt, .rfa, .rft, .rte, .dwg).")] string filePath,
        [Description("Response detail: summary (default) or full.")] string detail = "summary")
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return ToolHelpers.ErrorResult("filePath is required.");

        if (!File.Exists(filePath))
            return ToolHelpers.ErrorResult($"File not found: {filePath}");

        try
        {
            var reader = catalog.GetReader(filePath);
            var result = reader.Read(new FileInfoRequest(filePath, ParseDetail(detail)));
            return SerializeFileInfo(result);
        }
        catch (FileReadException ex) when (ex.Error == FileError.UnsupportedFormat)
        {
            return ToolHelpers.ErrorResult($"Unsupported file extension '{Path.GetExtension(filePath)}'. Supported: {catalog.FormatSupportedExtensions()}");
        }
        catch (Exception ex)
        {
            return ToolHelpers.ErrorResult($"Failed to read file: {ex.Message}");
        }
    }

    private static CallToolResult SerializeFileInfo(FileInfoResult result) =>
        DynamicToolResults.Json(
            JsonSerializer.Serialize(result, result.GetType(), McpToolJson.Options),
            JsonSerializer.SerializeToElement(result, result.GetType(), McpToolJson.Options));

    private static FileInfoDetail ParseDetail(string detail) =>
        string.Equals(detail, "full", StringComparison.OrdinalIgnoreCase)
            ? FileInfoDetail.Full
            : FileInfoDetail.Summary;
}
