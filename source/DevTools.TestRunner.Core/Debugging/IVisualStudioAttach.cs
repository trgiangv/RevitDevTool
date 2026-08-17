namespace DevTools.TestRunner.Core.Debugging;

public interface IVisualStudioAttach
{
    bool TryAttach(int hostProcessId, int? parentProcessId, TextWriter warnings);

    void TryDetach(int hostProcessId, TextWriter warnings);
}
