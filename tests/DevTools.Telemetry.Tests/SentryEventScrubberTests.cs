using Sentry;
using Sentry.Protocol;

namespace DevTools.Telemetry.Tests;

[TestClass]
public sealed class SentryEventScrubberTests
{
    [TestMethod]
    public void Scrub_removes_server_name_and_paths()
    {
        var frame = new SentryStackFrame
        {
            FileName = @"C:\Users\me\scripts\tool.cs",
            AbsolutePath = @"C:\Users\me\scripts\tool.cs",
        };
        var evt = new SentryEvent
        {
            ServerName = "MY-PC",
            Message = new SentryMessage { Message = @"Open D:\Projects\Tower.rvt failed" },
            SentryExceptions =
            [
                new SentryException
                {
                    Value = @"File C:\Users\me\a.py not found",
                    Stacktrace = new SentryStackTrace { Frames = [frame] },
                },
            ],
        };

        SentryEventScrubber.Scrub(evt);

        Assert.IsNull(evt.ServerName);
        Assert.DoesNotContain("Projects", evt.Message!.Message!, StringComparison.Ordinal);
        var exception = evt.SentryExceptions!.Single();
        Assert.DoesNotContain(@"\Users\me", exception.Value!, StringComparison.Ordinal);
        Assert.Contains("[path]", exception.Value!, StringComparison.Ordinal);
        Assert.IsNull(frame.AbsolutePath);
        Assert.AreEqual("tool.cs", frame.FileName);
    }
}
