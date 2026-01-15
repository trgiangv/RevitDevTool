namespace RevitDevTool.Logging.Enums;

[Flags]
public enum RevitEnricher
{
    None = 0,
    RevitVersion = 1 << 0,
    RevitBuild = 1 << 1,
    RevitUserName = 1 << 2,
    RevitLanguage = 1 << 3,
    RevitDocumentTitle = 1 << 4,
    RevitDocumentPathName = 1 << 5,
    RevitDocumentModelPath = 1 << 6
}