using System.Text.Json;
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
        var seconds = TestingHostTiming.ComputeAdapterRunnerProcessTimeoutSeconds(
            launchTimeoutSeconds: 360,
            runTimeoutSeconds: 60);

        Assert.Equal(360 + 60 + TestingHostTiming.RunnerProcessTimeoutSlackSeconds, seconds);
        Assert.Equal(450, seconds);
    }

    [Fact]
    public void Effective_request_timeout_prefers_scaled_request_field()
    {
        var options = new TestingHostOptions("Revit", "2025", false, 60, 180, null, RequestTimeoutSeconds: 180);
        Assert.Equal(60, options.PerTestTimeoutSeconds);
        Assert.Equal(180, options.EffectiveRequestTimeoutSeconds);
        Assert.Equal(60, new TestingHostOptions("Revit", "2025", false, 60, 180, null).EffectiveRequestTimeoutSeconds);
    }

    [Fact]
    public void Output_drain_budgets_are_local_io_and_not_host_options()
    {
        Assert.Equal(5_000, TestingHostTiming.TimedOutProcessOutputDrainMilliseconds);
        Assert.Equal(30_000, TestingHostTiming.ExitedProcessOutputDrainMilliseconds);
    }

    [Fact]
    public void SerializeInvocation_forwards_timeouts_without_adding_adapter_slack()
    {
        var json = TestingRunnerCli.SerializeInvocation(
            new TestingRunRequest(
                TestingProtocol.CurrentVersion,
                Guid.Empty,
                "provider.example",
                new TestingAssemblyReference("C:\\tests\\Sample.dll"),
                TestingSelection.All),
            new TestingHostOptions("Revit", "2025", false, 60, 360, null));
        var invocation = JsonSerializer.Deserialize(json, TestingJsonContext.Default.TestingRunInvocation);

        Assert.NotNull(invocation);
        Assert.Equal(60, invocation.Host.PerTestTimeoutSeconds);
        Assert.Equal(360, invocation.Host.LaunchTimeoutSeconds);
        Assert.DoesNotContain("450", json, StringComparison.Ordinal);
    }
}
