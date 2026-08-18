using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Transport;

namespace DevTools.Testing.Transport.Tests;

public sealed class TestingHostTimingTests
{
    [Fact]
    public void ScalePerTestTimeout_multiplies_per_test_budget_by_case_count()
    {
        Assert.Equal(60, TestingHostTiming.ScalePerTestTimeoutSeconds(60, 0));
        Assert.Equal(60, TestingHostTiming.ScalePerTestTimeoutSeconds(60, 1));
        Assert.Equal(180, TestingHostTiming.ScalePerTestTimeoutSeconds(60, 3));
    }

    [Fact]
    public void Adapter_runner_budget_uses_csproj_host_options_plus_local_slack()
    {
        // Sample: LaunchTimeout=360, PerTestTimeout=60 × 1 test; slack is not an MSBuild property.
        var seconds = TestingHostTiming.ComputeAdapterRunnerProcessTimeoutSeconds(
            launchTimeoutSeconds: 360,
            runTimeoutSeconds: 60);

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
                "provider.example",
                new TestingAssemblyReference("C:\\tests\\Sample.dll", null, null),
                new TestingSelection([]),
                new Dictionary<string, string>()),
            new TestingHostOptions("Revit", "2025", false, 60, 360, null));

        Assert.Contains("--per-test-timeout", args);
        Assert.Equal("60", args[args.IndexOf("--per-test-timeout") + 1]);
        Assert.Contains("--launch-timeout", args);
        Assert.Equal("360", args[args.IndexOf("--launch-timeout") + 1]);
        Assert.DoesNotContain("450", args);
        Assert.DoesNotContain("30", args);
    }

    [Fact]
    public void Cli_emits_test_ids_and_provider_payload()
    {
        var args = TestingRunnerCli.BuildRunArguments(
            new TestingRunRequest(
                TestingProtocol.CurrentVersion,
                Guid.Empty,
                "provider.example",
                new TestingAssemblyReference("C:\\tests\\Sample.dll", null, null),
                new TestingSelection(["HostSmokeTests.Arithmetic"], "<filter><name>Arithmetic</name></filter>"),
                new Dictionary<string, string>()),
            new TestingHostOptions("Revit", "2025", false, 60, 180, null));

        Assert.Contains("--test", args);
        Assert.Equal("""["HostSmokeTests.Arithmetic"]""", args[args.IndexOf("--test") + 1]);
        Assert.Contains("--filter", args);
        Assert.Equal("<filter><name>Arithmetic</name></filter>", args[args.IndexOf("--filter") + 1]);
    }
}
