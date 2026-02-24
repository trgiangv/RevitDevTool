using System.Text.RegularExpressions;

namespace RevitDevTool.Console.RevitFileInfo.BasicFileStream;

public class BasicFileInfo
{
    public const string BasicFileInfoName = "BasicFileInfo";

    public string? LastSavePath { get; set; }
    public string? CentralPath { get; set; }
    public bool IsModified { get; set; }
    public bool IsWorkshared { get; set; }
    public WorksharingType WorksharingType { get; set; }
    public bool IsSingleUserCloudModel { get; set; }
    public bool IsRevitLite { get; set; }
    public string? Username { get; set; }
    public string? Author { get; set; }
    public int DefaultOpenWorkset { get; set; }
    public int FileVersion { get; set; }
    public LanguageCode FileLocale { get; set; } = LanguageCode.Unknown;
    public ModelIdentity Identity { get; set; } = ModelIdentity.Empty;
    public ModelIdentity CentralIdentity { get; set; } = ModelIdentity.Empty;
    public ApplicationInfo AppInfo { get; set; } = new();
    public ModelVersionInfo CurrentVersion { get; set; } = ModelVersionInfo.Empty;
    public ModelVersionInfo CentralVersion { get; set; } = ModelVersionInfo.Empty;

    internal static BasicFileInfo ReadFromReader(BinaryReader reader)
    {
        return ReadBasicFileInfo(reader, new BasicFileInfo());
    }

    private static BasicFileInfo ReadBasicFileInfo(BinaryReader reader, BasicFileInfo info)
    {
        info.FileVersion = reader.ReadInt32();
        info.IsWorkshared = reader.ReadBoolean();

        var worksharingType = reader.ReadWorksharingType();
        info.WorksharingType = info.IsWorkshared ? worksharingType : WorksharingType.NotEnabled;

        info.Username = reader.ReadValueString();
        info.CentralPath = reader.ReadValueString();

        if (info.FileVersion >= FormatConstants.Format)
        {
            info.AppInfo.Format = reader.ReadValueString();
            info.AppInfo.Build = reader.ReadValueString();
        }
        else
        {
            info.AppInfo.Build = reader.ReadValueString() ?? "2014";
            info.AppInfo.Format = Regex.Match(info.AppInfo.Build, @"20\d\d").Value;
        }

        if (info.FileVersion >= FormatConstants.LastSavePath)
            info.LastSavePath = reader.ReadValueString();

        if (info.FileVersion >= FormatConstants.DefaultOpenWorkset)
            info.DefaultOpenWorkset = reader.ReadInt32();

        if (info.FileVersion >= FormatConstants.IsRevitLite)
            info.IsRevitLite = reader.ReadBoolean();

        if (info.FileVersion >= FormatConstants.CentralIdentity)
            info.CentralIdentity = reader.ReadIdentity();

        if (info.FileVersion >= FormatConstants.FileLocale)
            info.FileLocale = reader.ReadLanguageCode();

        if (info.FileVersion >= FormatConstants.IsModified)
            info.IsModified = reader.ReadBoolean();

        if (info.FileVersion >= FormatConstants.CentralVersion)
            info.CentralVersion = reader.ReadCentralVersion();

        if (info.FileVersion >= FormatConstants.CurrentVersion)
            info.CurrentVersion = reader.ReadCurrentVersion();

        if (info.FileVersion >= FormatConstants.Identity)
            info.Identity = reader.ReadIdentity();

        if (info.FileVersion >= FormatConstants.IsSingleUserCloudModel)
            info.IsSingleUserCloudModel = reader.ReadBoolean();

        if (info.FileVersion >= FormatConstants.Author)
            info.Author = reader.ReadValueString();

        if (info.FileVersion >= FormatConstants.ClientAppName)
            info.AppInfo.ClientAppName = reader.ReadValueString();

        return info;
    }
}
