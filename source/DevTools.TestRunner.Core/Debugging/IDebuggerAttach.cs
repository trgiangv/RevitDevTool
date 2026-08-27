namespace DevTools.TestRunner.Core.Debugging;

public readonly record struct AttachTarget(
    int HostProcessId,
    int? ParentProcessId,
    string? AssemblyPath);

public interface IDebuggerAttach
{
    bool TryAttach(AttachTarget target, TextWriter warnings);

    void TryDetach(int hostProcessId, TextWriter warnings);
}
