namespace DevTools.TestRunner.Core.Debugging;

public sealed class DebugAttachScope : IDisposable
{
    private readonly IDebuggerAttach _attach;
    private readonly int _hostProcessId;
    private readonly TextWriter _warnings;
    private bool _disposed;

    private DebugAttachScope(IDebuggerAttach attach, int hostProcessId, TextWriter warnings)
    {
        _attach = attach;
        _hostProcessId = hostProcessId;
        _warnings = warnings;
    }

    public static DebugAttachScope? TryBegin(
        bool enabled,
        AttachTarget target,
        IDebuggerAttach attach,
        TextWriter warnings)
    {
        if (!enabled)
            return null;

        ArgumentNullException.ThrowIfNull(attach);
        ArgumentNullException.ThrowIfNull(warnings);

        attach.TryAttach(target, warnings);
        return new DebugAttachScope(attach, target.HostProcessId, warnings);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _attach.TryDetach(_hostProcessId, _warnings);
    }
}
