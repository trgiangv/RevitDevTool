namespace DevTools.TestRunner.Debugging;

public readonly record struct AttachTarget(
    int HostProcessId,
    int? ParentProcessId);

public interface IDebuggerAttach
{
    bool TryAttach(AttachTarget target, TextWriter warnings);
}
