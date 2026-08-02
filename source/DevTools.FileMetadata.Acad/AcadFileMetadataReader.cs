using ACadSharp.IO;
using DevTools.FileMetadata.Core;

namespace DevTools.FileMetadata.Acad;

public sealed class AcadFileMetadataReader : IFileReader
{
    public IReadOnlyList<string> SupportedExtensions { get; } = [".dwg"];

    public FileInfoResult Read(FileInfoRequest request)
    {
        var full = ReadFull(request.FilePath);
        return request.Detail == FileInfoDetail.Full ? full : ToSummary(full);
    }

    private static DwgFileInfoResult ReadFull(string filePath)
    {
        using var reader = new DwgReader(filePath);
        var doc = reader.Read();
        return new DwgFileInfoResult
        {
            HostApplication = FileHostApplication.AutoCad,
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),
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
                .ToArray()
        };
    }

    private static DwgFileInfoSummaryResult ToSummary(DwgFileInfoResult full) =>
        new()
        {
            HostApplication = full.HostApplication,
            FilePath = full.FilePath,
            FileName = full.FileName,
            AcadVersion = full.AcadVersion,
            Title = full.Title,
            Author = full.Author,
            LayerCount = full.LayerCount,
            BlockCount = full.BlockCount
        };
}
