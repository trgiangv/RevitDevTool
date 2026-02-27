namespace RevitDevTool.Scintilla.Contracts;

public interface ILogRenderStrategy
{
    string FormatLine(LogEntry entry);
    int GetStyleId(LogSeverity severity);
    void ConfigureStyles(IStyleWriter styleWriter);
}
