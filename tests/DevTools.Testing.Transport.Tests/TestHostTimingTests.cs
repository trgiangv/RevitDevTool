using System.Text.Json;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Transport;

namespace DevTools.Testing.Transport.Tests;

public sealed class TestHostTimingTests
{
    [Fact]
    public void ScalePerTestTimeout_multiplies_per_test_budget_by_case_count()
    {
        Assert.Equal(60, TestHostTiming.ScalePerTestTimeoutSeconds(60, 0));
        Assert.Equal(60, TestHostTiming.ScalePerTestTimeoutSeconds(60, 1));
        Assert.Equal(180, TestHostTiming.ScalePerTestTimeoutSeconds(60, 3));
    }

    [Fact]
    public void Adapter_runner_budget_uses_csproj_host_options_plus_local_slack()
    {
        var seconds = TestHostTiming.ComputeAdapterRunnerProcessTimeoutSeconds(
            launchTimeoutSeconds: 360,
            runTimeoutSeconds: 60);

        Assert.Equal(360 + 60 + TestHostTiming.RunnerProcessTimeoutSlackSeconds, seconds);
        Assert.Equal(450, seconds);
    }

    [Fact]
    public void Effective_request_timeout_prefers_scaled_request_field()
    {
        var options = new TestHostOptions("Revit", "2025", false, 60, 180, RequestTimeoutSeconds: 180);
        Assert.Equal(60, options.PerTestTimeoutSeconds);
        Assert.Equal(180, options.EffectiveRequestTimeoutSeconds);
        Assert.Equal(60, new TestHostOptions("Revit", "2025", false, 60, 180).EffectiveRequestTimeoutSeconds);
    }

    [Fact]
    public void Output_drain_budgets_are_local_io_and_not_host_options()
    {
        Assert.Equal(5_000, TestHostTiming.TimedOutProcessOutputDrainMilliseconds);
        Assert.Equal(30_000, TestHostTiming.ExitedProcessOutputDrainMilliseconds);
    }

    [Fact]
    public void SerializeExecute_forwards_timeouts_without_adding_adapter_slack()
    {
        var json = TestRunnerCli.SerializeExecute(
            new TestRunRequest(
                TestingProtocol.CurrentVersion,
                Guid.Empty,
                TestFrameworkId.NUnit,
                new TestAssemblyReference("C:\\tests\\Sample.dll"),
                TestSelection.All),
            new TestHostOptions("Revit", "2025", false, 60, 360));
        var invocation = JsonSerializer.Deserialize(json, TestingJsonContext.Default.TestRunExecute);

        Assert.NotNull(invocation);
        Assert.Equal(60, invocation.Host.PerTestTimeoutSeconds);
        Assert.Equal(360, invocation.Host.LaunchTimeoutSeconds);
        Assert.DoesNotContain("450", json, StringComparison.Ordinal);
    }
}
