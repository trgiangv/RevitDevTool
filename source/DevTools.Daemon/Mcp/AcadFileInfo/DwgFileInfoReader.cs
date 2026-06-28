using ACadSharp.IO;

namespace DevTools.Daemon.Mcp.AcadFileInfo;

/// <summary>
/// Reads metadata from DWG files without AutoCAD via ACadSharp.
/// </summary>
internal static class DwgFileInfoReader
{
    public static DwgFileInfo Read(string filePath)
    {
        using var reader = new DwgReader(filePath);
        var doc = reader.Read();

        var info = new DwgFileInfo
        {
            AcadVersion = doc.Header.Version.ToString(),
            Title = doc.SummaryInfo?.Title,
            Subject = doc.SummaryInfo?.Subject,
            Author = doc.SummaryInfo?.Author,
            Keywords = doc.SummaryInfo?.Keywords,
            Comments = doc.SummaryInfo?.Comments,
            LastSavedBy = doc.SummaryInfo?.LastSavedBy,
            LayerCount = doc.Layers.Count,
            BlockCount = doc.BlockRecords.Count,
            Layers = doc.Layers
                .Select(l => new DwgLayerInfo { Name = l.Name, IsOn = l.IsOn })
                .OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };

        return info;
    }
}

public sealed class DwgFileInfo
{
    public string? AcadVersion { get; init; }
    public string? Title { get; init; }
    public string? Subject { get; init; }
    public string? Author { get; init; }
    public string? Keywords { get; init; }
    public string? Comments { get; init; }
    public string? LastSavedBy { get; init; }
    public int LayerCount { get; init; }
    public int BlockCount { get; init; }
    public List<DwgLayerInfo> Layers { get; init; } = [];
}

public sealed class DwgLayerInfo
{
    public string Name { get; init; } = string.Empty;
    public bool IsOn { get; init; }
}
