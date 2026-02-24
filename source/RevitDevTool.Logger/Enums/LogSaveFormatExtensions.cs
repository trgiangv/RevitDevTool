namespace RevitDevTool.Logger.Enums;

public static class LogSaveFormatExtensions
{
    public static string ToFileExtension(this LogSaveFormat format) => format switch
    {
        LogSaveFormat.Json => "json",
        _ => "log"
    };
}
