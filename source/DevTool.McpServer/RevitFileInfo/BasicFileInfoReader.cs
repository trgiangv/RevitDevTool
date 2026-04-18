using System.Text;
using System.Text.RegularExpressions;
using OpenMcdf;

namespace DevTool.McpServer.RevitFileInfo;

internal static partial class BasicFileInfoReader
{
    private const int FormatVersion = 12;
    private const int LastSavePathVersion = 1;
    private const int DefaultOpenWorksetVersion = 3;
    private const int IsRevitLiteVersion = 4;
    private const int CentralIdentityVersion = 5;
    private const int FileLocaleVersion = 6;
    private const int IsModifiedVersion = 7;
    private const int CentralVersionNum = 8;
    private const int CurrentVersionNum = 9;
    private const int IdentityVersion = 10;
    private const int IsSingleUserCloudModelVersion = 11;
    private const int AuthorVersion = 13;

    public static BasicFileInfoDto? Read(string filePath)
    {
        using var storage = RootStorage.OpenRead(filePath);
        CfbStream stream;
        try
        {
            stream = storage.OpenStream("BasicFileInfo");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or FormatException)
        {
            return null;
        }

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        ms.Position = 0;

        using var reader = new BinaryReader(ms, Encoding.Unicode);
        var dto = new BasicFileInfoDto
        {
            FileVersion = reader.ReadInt32(),
            IsWorkshared = reader.ReadBoolean()
        };

        var worksharingByte = reader.ReadByte();
        dto.WorksharingType = dto.IsWorkshared
            ? WorksharingTypeToString(worksharingByte + 1)
            : "Not enabled";

        dto.Username = ReadString(reader);
        dto.CentralPath = ReadString(reader);

        if (dto.FileVersion >= FormatVersion)
        {
            dto.RevitVersion = ReadString(reader);
            dto.Build = ReadString(reader);
        }
        else
        {
            dto.Build = ReadString(reader);
            dto.RevitVersion = ExtractVersionFromBuild(dto.Build);
        }

        if (dto.FileVersion >= LastSavePathVersion)
            dto.LastSavePath = ReadString(reader);
        if (dto.FileVersion >= DefaultOpenWorksetVersion)
            dto.DefaultOpenWorkset = reader.ReadInt32();
        if (dto.FileVersion >= IsRevitLiteVersion)
            dto.IsRevitLite = reader.ReadBoolean();
        if (dto.FileVersion >= CentralIdentityVersion)
            dto.CentralIdentity = ReadString(reader);
        if (dto.FileVersion >= FileLocaleVersion)
            dto.Locale = ReadString(reader);
        if (dto.FileVersion >= IsModifiedVersion)
            dto.IsModified = reader.ReadBoolean();

        if (dto.FileVersion >= CentralVersionNum)
        {
            reader.ReadInt32();
            ReadString(reader);
        }

        if (dto.FileVersion >= CurrentVersionNum)
        {
            ReadString(reader);
            ReadString(reader);
        }

        if (dto.FileVersion >= IdentityVersion)
            dto.ModelIdentity = ReadString(reader);
        if (dto.FileVersion >= IsSingleUserCloudModelVersion)
            dto.IsSingleUserCloud = reader.ReadBoolean();
        if (dto.FileVersion >= AuthorVersion)
            dto.Author = ReadString(reader);

        return dto;
    }

    private static string? ReadString(BinaryReader reader)
    {
        var length = reader.ReadInt32();
        if (length <= 0) return null;
        return new string(reader.ReadChars(length)).TrimEnd('\0');
    }

    private static string WorksharingTypeToString(int value) => value switch
    {
        1 => "Not enabled",
        2 => "Central",
        3 => "Local",
        4 => "In progress",
        5 => "Created Local",
        _ => $"Unknown ({value})"
    };

    private static string? ExtractVersionFromBuild(string? build)
    {
        if (string.IsNullOrWhiteSpace(build))
            return null;
        var match = VersionRegex().Match(build);
        return match.Success ? match.Value : null;
    }

    [GeneratedRegex(@"20\d\d")]
    private static partial Regex VersionRegex();
}

internal sealed class BasicFileInfoDto
{
    public int FileVersion { get; set; }
    public string? RevitVersion { get; set; }
    public string? Build { get; set; }
    public bool IsWorkshared { get; set; }
    public string WorksharingType { get; set; } = "Not enabled";
    public string? Username { get; set; }
    public string? CentralPath { get; set; }
    public string? LastSavePath { get; set; }
    public int DefaultOpenWorkset { get; set; }
    public bool IsRevitLite { get; set; }
    public bool IsModified { get; set; }
    public bool IsSingleUserCloud { get; set; }
    public string? Locale { get; set; }
    public string? Author { get; set; }
    public string? ModelIdentity { get; set; }
    public string? CentralIdentity { get; set; }
}
