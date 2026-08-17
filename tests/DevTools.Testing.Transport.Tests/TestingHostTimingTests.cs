using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Transport;

namespace DevTools.Testing.Transport.Tests;

public sealed class TestingHostTimingTests
{
    [Fact]
    public void Adapter_runner_budget_uses_csproj_host_options_plus_local_slack()
    {
        // Sample csproj HostLaunchTimeout=360, HostTimeout=60; slack is not an MSBuild property.
        var seconds = TestingHostTiming.ComputeAdapterRunnerProcessTimeoutSeconds(
            hostLaunchTimeoutSeconds: 360,
            hostTimeoutSeconds: 60);

        Assert.Equal(360 + 60 + TestingHostTiming.RunnerProcessTimeoutSlackSeconds, seconds);
        Assert.Equal(450, seconds);
    }

    [Fact]
    public void Output_drain_budgets_are_local_io_and_not_host_options()
    {
        Assert.Equal(5_000, TestingHostTiming.TimedOutProcessOutputDrainMilliseconds);
        Assert.Equal(30_000, TestingHostTiming.ExitedProcessOutputDrainMilliseconds);
    }

    [Fact]
    public void Cli_forwards_csproj_timeouts_without_adding_adapter_slack()
    {
        var args = TestingRunnerCli.BuildRunArguments(
            new TestingRunRequest(
                TestingProtocol.CurrentVersion,
                Guid.Empty,
                TestingFrameworkIds.NUnit,
                new TestingAssemblyReference("C:\\tests\\Sample.dll", null, null),
                new TestingSelection([]),
                new Dictionary<string, string>()),
            new TestingHostOptions("Revit", "2025", false, 60, 360, null));

        Assert.Contains("--host-timeout", args);
        Assert.Equal("60", args[args.IndexOf("--host-timeout") + 1]);
        Assert.Contains("--host-launch-timeout", args);
        Assert.Equal("360", args[args.IndexOf("--host-launch-timeout") + 1]);
        Assert.DoesNotContain("450", args);
        Assert.DoesNotContain("30", args);
    }
}
