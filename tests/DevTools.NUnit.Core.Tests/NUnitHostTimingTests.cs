using DevTools.NUnit.Core;

namespace DevTools.NUnit.Core.Tests;

public sealed class NUnitHostTimingTests
{
    [Fact]
    public void Adapter_runner_budget_always_includes_host_launch_timeout()
    {
        // HostLaunch=false still cold-starts when no matching pipe exists, so 360 must count.
        var seconds = NUnitHostTiming.ComputeAdapterRunnerProcessTimeoutSeconds(
            hostLaunchTimeoutSeconds: 360,
            hostTimeoutSeconds: 60);

        Assert.Equal(360 + 60 + NUnitHostTiming.RunnerProcessTimeoutSlackSeconds, seconds);
        Assert.Equal(450, seconds);
    }
}
