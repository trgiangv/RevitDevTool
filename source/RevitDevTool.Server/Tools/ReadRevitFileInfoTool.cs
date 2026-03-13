using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using OpenMcdf;

namespace RevitDevTool.Server.Tools;

public sealed partial class ReadRevitFileInfoTool : McpServerTool
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
            var info = ReadBasicFileInfo(filePath);
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

    private static object ReadBasicFileInfo(string filePath)
    {
        using var cf = new CompoundFile(filePath);
        CFStream? stream;
        try { stream = cf.RootStorage.GetStream("BasicFileInfo"); }
        catch { return new { error = "BasicFileInfo stream not found in file." }; }

        var bytes = stream.GetData();
        using var ms = new MemoryStream(bytes);
        using var reader = new BinaryReader(ms, Encoding.Unicode);

        var fileVersion = reader.ReadInt32();
        var isWorkshared = reader.ReadBoolean();

        reader.ReadByte();
        reader.ReadByte();
        reader.ReadByte();

        var username = ReadUtf16String(reader);
        var centralPath = ReadUtf16String(reader);

        string? format = null;

        if (fileVersion >= 4)
        {
            format = ReadUtf16String(reader);
        }
        var build = ReadUtf16String(reader);

        string? lastSavePath = null;
        if (fileVersion >= 5)
            lastSavePath = ReadUtf16String(reader);

        return new
        {
            filePath,
            fileName = Path.GetFileName(filePath),
            fileVersion,
            revitVersion = format ?? ExtractVersionFromBuild(build),
            build,
            isWorkshared,
            username,
            centralPath,
            lastSavePath
        };
    }

    private static string ReadUtf16String(BinaryReader reader)
    {
        var length = reader.ReadInt32();
        if (length <= 0) return string.Empty;
        var chars = reader.ReadChars(length);
        return new string(chars).TrimEnd('\0');
    }

    private static string? ExtractVersionFromBuild(string? build)
    {
        if (build is null) return null;
        var match = VersionRegex().Match(build);
        return match.Success ? match.Value : null;
    }

    private static CallToolResult ErrorResult(string message) =>
        new() { IsError = true, Content = [new TextContentBlock { Text = message }] };

    [GeneratedRegex(@"20\d\d")]
    private static partial Regex VersionRegex();
}
