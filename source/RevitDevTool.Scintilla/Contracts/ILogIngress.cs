namespace RevitDevTool.Scintilla.Contracts;

public interface ILogIngress
{
    bool TryPost(LogEntry entry);
    long DroppedMessages { get; }
}
