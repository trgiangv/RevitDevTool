using RevitDevTool.Scintilla.Core;
namespace RevitDevTool.Scintilla.Logger;

internal interface ILogEntrySink
{
    bool TryPost(LogEntry entry);
}
