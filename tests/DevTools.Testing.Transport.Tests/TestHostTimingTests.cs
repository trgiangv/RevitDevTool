using System.Text.Json;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Transport;

namespace DevTools.Testing.Transport.Tests;

[TestClass]
public sealed class TestHostTimingTests
{
    [TestMethod]
    public void ScalePerTestTimeout_multiplies_per_test_budget_by_case_count()
    {
        Assert.AreEqual(60, TestHostTiming.ScalePerTestTimeoutSeconds(60, 0));
        Assert.AreEqual(60, TestHostTiming.ScalePerTestTimeoutSeconds(60, 1));
        Assert.AreEqual(180, TestHostTiming.ScalePerTestTimeoutSeconds(60, 3));
    }

    [TestMethod]
    public void Adapter_runner_budget_uses_csproj_host_options_plus_local_slack()
    {
        var seconds = TestHostTiming.ComputeAdapterRunnerProcessTimeoutSeconds(
            launchTimeoutSeconds: 360,
            runTimeoutSeconds: 60);

        Assert.AreEqual(360 + 60 + TestHostTiming.RunnerProcessTimeoutSlackSeconds, seconds);
        Assert.AreEqual(450, seconds);
    }

    [TestMethod]
    public void Effective_request_timeout_prefers_scaled_request_field()
    {
        var options = new TestHostOptions("Revit", "2025", false, 60, 180, RequestTimeoutSeconds: 180);
        Assert.AreEqual(60, options.PerTestTimeoutSeconds);
        Assert.AreEqual(180, options.EffectiveRequestTimeoutSeconds);
        Assert.AreEqual(60, new TestHostOptions("Revit", "2025", false, 60, 180).EffectiveRequestTimeoutSeconds);
    }

    [TestMethod]
    public void Output_drain_budgets_are_local_io_and_not_host_options()
    {
#pragma warning disable MSTEST0032 // const timing budgets document the drain contract.
        Assert.AreEqual(5_000, TestHostTiming.TimedOutProcessOutputDrainMilliseconds);
        Assert.AreEqual(30_000, TestHostTiming.ExitedProcessOutputDrainMilliseconds);
#pragma warning restore MSTEST0032
    }

    [TestMethod]
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

        Assert.IsNotNull(invocation);
        Assert.AreEqual(60, invocation.Host.PerTestTimeoutSeconds);
        Assert.AreEqual(360, invocation.Host.LaunchTimeoutSeconds);
        Assert.DoesNotContain("450", json, StringComparison.Ordinal);
    }
}
