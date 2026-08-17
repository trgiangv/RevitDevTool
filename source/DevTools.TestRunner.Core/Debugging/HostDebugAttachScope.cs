namespace DevTools.TestRunner.Core.Debugging;

public sealed class HostDebugAttachScope : IDisposable
{
    private readonly IVisualStudioAttach _attach;
    private readonly int _hostProcessId;
    private readonly TextWriter _warnings;
    private bool _disposed;

    private HostDebugAttachScope(IVisualStudioAttach attach, int hostProcessId, TextWriter warnings)
    {
        _attach = attach;
        _hostProcessId = hostProcessId;
        _warnings = warnings;
    }

    public static HostDebugAttachScope? TryBegin(
        bool enabled,
        int hostProcessId,
        int? parentProcessId,
        IVisualStudioAttach attach,
        TextWriter warnings)
    {
        if (!enabled)
            return null;

        ArgumentNullException.ThrowIfNull(attach);
        ArgumentNullException.ThrowIfNull(warnings);

        attach.TryAttach(hostProcessId, parentProcessId, warnings);
        return new HostDebugAttachScope(attach, hostProcessId, warnings);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _attach.TryDetach(_hostProcessId, _warnings);
    }
}
