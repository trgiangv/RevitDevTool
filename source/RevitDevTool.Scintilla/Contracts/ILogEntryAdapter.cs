namespace RevitDevTool.Scintilla.Contracts;

public interface ILogEntryAdapter<in TEvent>
{
    LogEntry Adapt(TEvent source);
}
