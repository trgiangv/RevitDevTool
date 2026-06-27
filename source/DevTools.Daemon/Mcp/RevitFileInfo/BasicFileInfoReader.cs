using System.Text;
using System.Text.RegularExpressions;

namespace DevTools.Daemon.Mcp.RevitFileInfo;

internal static partial class BasicFileInfoReader
{
    private const string StreamName = "BasicFileInfo";

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

    public static BasicFileInfo? Read(RevitCompoundFile file)
    {
        using var ms = file.TryReadStream(StreamName);
        if (ms is null) return null;

        using var reader = new BinaryReader(ms, Encoding.Unicode);
        var header = ReadHeader(reader);
        var optional = ReadOptionalFields(reader, header.FileVersion);

        return header with
        {
            RevitVersion = optional.RevitVersion,
            Build = optional.Build,
            LastSavePath = optional.LastSavePath,
            DefaultOpenWorkset = optional.DefaultOpenWorkset,
            IsRevitLite = optional.IsRevitLite,
            CentralIdentity = optional.CentralIdentity,
            Locale = optional.Locale,
            IsModified = optional.IsModified,
            ModelIdentity = optional.ModelIdentity,
            IsSingleUserCloud = optional.IsSingleUserCloud,
            Author = optional.Author,
        };
    }

    private static BasicFileInfo ReadHeader(BinaryReader reader)
    {
        var fileVersion = reader.ReadInt32();
        var isWorkshared = reader.ReadBoolean();
        var worksharingByte = reader.ReadByte();

        return new BasicFileInfo
        {
            FileVersion = fileVersion,
            IsWorkshared = isWorkshared,
            WorksharingType = isWorkshared ? WorksharingTypeToString(worksharingByte + 1) : "Not enabled",
            Username = ReadString(reader),
            CentralPath = ReadString(reader),
        };
    }

    private static BasicFileInfo ReadOptionalFields(BinaryReader reader, int ver)
    {
        string? revitVersion, build;
        if (ver >= FormatVersion)
        {
            revitVersion = ReadString(reader);
            build = ReadString(reader);
        }
        else
        {
            build = ReadString(reader);
            revitVersion = ExtractVersionFromBuild(build);
        }

        var lastSavePath = ver >= LastSavePathVersion ? ReadString(reader) : null;
        var defaultOpenWorkset = ver >= DefaultOpenWorksetVersion ? reader.ReadInt32() : 0;
        var isRevitLite = ver >= IsRevitLiteVersion && reader.ReadBoolean();
        var centralIdentity = ver >= CentralIdentityVersion ? ReadString(reader) : null;
        var locale = ver >= FileLocaleVersion ? ReadString(reader) : null;
        var isModified = ver >= IsModifiedVersion && reader.ReadBoolean();

        if (ver >= CentralVersionNum) { reader.ReadInt32(); ReadString(reader); }
        if (ver >= CurrentVersionNum) { ReadString(reader); ReadString(reader); }

        var modelIdentity = ver >= IdentityVersion ? ReadString(reader) : null;
        var isSingleUserCloud = ver >= IsSingleUserCloudModelVersion && reader.ReadBoolean();
        var author = ver >= AuthorVersion ? ReadString(reader) : null;

        return new BasicFileInfo
        {
            RevitVersion = revitVersion,
            Build = build,
            LastSavePath = lastSavePath,
            DefaultOpenWorkset = defaultOpenWorkset,
            IsRevitLite = isRevitLite,
            CentralIdentity = centralIdentity,
            Locale = locale,
            IsModified = isModified,
            ModelIdentity = modelIdentity,
            IsSingleUserCloud = isSingleUserCloud,
            Author = author,
        };
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

[PublicAPI]
internal sealed record BasicFileInfo
{
    public int FileVersion { get; init; }
    public string? RevitVersion { get; init; }
    public string? Build { get; init; }
    public bool IsWorkshared { get; init; }
    public string WorksharingType { get; init; } = "Not enabled";
    public string? Username { get; init; }
    public string? CentralPath { get; init; }
    public string? LastSavePath { get; init; }
    public int DefaultOpenWorkset { get; init; }
    public bool IsRevitLite { get; init; }
    public bool IsModified { get; init; }
    public bool IsSingleUserCloud { get; init; }
    public string? Locale { get; init; }
    public string? Author { get; init; }
    public string? ModelIdentity { get; init; }
    public string? CentralIdentity { get; init; }
}
