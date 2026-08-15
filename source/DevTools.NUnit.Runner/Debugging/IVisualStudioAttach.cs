namespace DevTools.NUnit.Runner.Debugging;

internal interface IVisualStudioAttach
{
    bool TryAttach(int hostProcessId, int? parentProcessId, TextWriter warnings);

    void TryDetach(int hostProcessId, TextWriter warnings);
}
