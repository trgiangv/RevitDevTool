using DevTools.NUnit.Runner.Debugging;

namespace DevTools.NUnit.Runner.Tests;

public sealed class HostDebugAttachScopeTests
{
    [Fact]
    public void TryBegin_skips_when_debug_is_disabled()
    {
        var attach = new RecordingAttach();
        using var warnings = new StringWriter();

        using var scope = HostDebugAttachScope.TryBegin(
            enabled: false,
            hostProcessId: 11,
            parentProcessId: 22,
            attach,
            warnings);

        Assert.Null(scope);
        Assert.Empty(attach.Calls);
    }

    [Fact]
    public void TryBegin_attaches_then_detach_on_dispose()
    {
        var attach = new RecordingAttach();
        using var warnings = new StringWriter();

        using (var scope = HostDebugAttachScope.TryBegin(
                   enabled: true,
                   hostProcessId: 11,
                   parentProcessId: 22,
                   attach,
                   warnings))
        {
            Assert.NotNull(scope);
            Assert.Equal(["attach:11:22"], attach.Calls);
        }

        Assert.Equal(["attach:11:22", "detach:11"], attach.Calls);
    }

    [Fact]
    public void TryBegin_still_detaches_when_attach_returns_false()
    {
        var attach = new RecordingAttach { AttachResult = false };
        using var warnings = new StringWriter();

        HostDebugAttachScope.TryBegin(true, 9, null, attach, warnings)!.Dispose();

        Assert.Equal(["attach:9:", "detach:9"], attach.Calls);
    }

    private sealed class RecordingAttach : IVisualStudioAttach
    {
        internal bool AttachResult { get; set; } = true;

        internal List<string> Calls { get; } = [];

        public bool TryAttach(int hostProcessId, int? parentProcessId, TextWriter warnings)
        {
            Calls.Add($"attach:{hostProcessId}:{parentProcessId}");
            return AttachResult;
        }

        public void TryDetach(int hostProcessId, TextWriter warnings) =>
            Calls.Add($"detach:{hostProcessId}");
    }
}
