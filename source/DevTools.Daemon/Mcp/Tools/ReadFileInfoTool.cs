using System.ComponentModel;
using System.Text.Json;
using DevTools.Daemon.Contracts;
using DevTools.Daemon.Hosts;
using DevTools.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Daemon.Mcp.Tools;

[McpServerToolType]
public sealed class ReadFileInfoTool
{
    private readonly HostDriverRegistry drivers;

    internal ReadFileInfoTool(HostDriverRegistry drivers)
    {
        this.drivers = drivers;
    }

    private static readonly string[] AllExtensions = [".rvt", ".rfa", ".rft", ".rte", ".dwg"];

    [McpServerTool(Name = "read_file_info")]
    [Description("Read metadata from a CAD/BIM file offline without launching a host. Revit files return version, worksets, links, and project info; AutoCAD DWG files return version, layers, blocks, and document properties.")]
    public async Task<CallToolResult> ReadAsync(
        [Description("Full path to a supported .rvt, .rfa, .rft, .rte, or .dwg file.")] string filePath,
        CancellationToken cancellationToken = default)
    {
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
