using DevTools.TestRunner.Core.Debugging;

namespace DevTools.TestRunner.Core.Tests;

public sealed class DebugAttachScopeTests
{
    [Fact]
    public void TryBegin_returns_null_when_debug_is_disabled()
    {
        var scope = DebugAttachScope.TryBegin(
            enabled: false,
            new AttachTarget(1, null, "assembly.dll"),
            new RecordingDebugger(),
            TextWriter.Null);

        Assert.Null(scope);
    }

    [Fact]
    public void TryBegin_attaches_and_detaches_when_disposed()
    {
        var debugger = new RecordingDebugger();
        using var scope = DebugAttachScope.TryBegin(
            enabled: true,
            new AttachTarget(42, 7, "assembly.dll"),
            debugger,
            TextWriter.Null);

        Assert.NotNull(scope);
        Assert.Equal((42, 7), debugger.Attached);
        scope!.Dispose();
        Assert.Equal(42, debugger.DetachedProcessId);
    }

    private sealed class RecordingDebugger : IDebuggerAttach
    {
        public (int HostPid, int? ParentPid)? Attached { get; private set; }
        public int? DetachedProcessId { get; private set; }

        public bool TryAttach(AttachTarget target, TextWriter warnings)
        {
            Attached = (target.HostProcessId, target.ParentProcessId);
            return true;
        }

        public void TryDetach(int hostProcessId, TextWriter warnings) => DetachedProcessId = hostProcessId;
    }
}
