using DevTools.FileMetadata.Core;

namespace DevTools.FileMetadata.Revit;

public sealed class RevitFileMetadataReader : IFileReader
{
    public IReadOnlyList<string> SupportedExtensions { get; } = [".rvt", ".rfa", ".rft", ".rte"];

    public FileInfoResult Read(FileInfoRequest request)
    {
        var full = ReadFull(request.FilePath);
        return request.Detail == FileInfoDetail.Full ? full : ToSummary(full);
    }

    public static string? TryReadRevitVersion(string filePath)
    {
        try
        {
            using var file = RevitCompoundFile.Open(filePath);
            return BasicFileInfoReader.Read(file)?.RevitVersion;
        }
        catch
        {
            return null;
        }
    }

    private static RevitFileInfoResult ReadFull(string filePath)
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
            HostApplication = FileHostApplication.Revit,
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

    private static RevitFileInfoSummaryResult ToSummary(RevitFileInfoResult full) =>
        new()
        {
            HostApplication = full.HostApplication,
            FilePath = full.FilePath,
            FileName = full.FileName,
            BasicInfo = new RevitBasicInfoSummary
            {
                FileVersion = full.BasicInfo.FileVersion,
                RevitVersion = full.BasicInfo.RevitVersion,
                IsWorkshared = full.BasicInfo.IsWorkshared,
                WorksharingType = full.BasicInfo.WorksharingType,
                Locale = full.BasicInfo.Locale
            },
            ProjectTitle = full.ProjectInformation?.Title,
            ProjectName = full.ProjectInformation?.Parameters.GetValueOrDefault("Project Name"),
            ExternalReferenceCount = full.TransmissionData?.ExternalFileReferences.Count ?? 0,
            ExternalReferences = full.TransmissionData?.ExternalFileReferences
                .Select(r => new ExternalReferenceSummary
                {
                    Type = r.ExternalFileReferenceType,
                    Path = r.LastSavedPath ?? r.DesiredPath
                })
                .ToArray(),
            WorksetCount = full.Worksets?.Count ?? 0
        };
}
