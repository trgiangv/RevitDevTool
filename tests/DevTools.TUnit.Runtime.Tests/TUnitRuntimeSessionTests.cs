using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Runtime;

namespace DevTools.TUnit.Runtime.Tests;

[TestClass]
[DoNotParallelize]
public sealed class TUnitRuntimeSessionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Names_return_invalid_request_because_the_testhost_mapper_resolves_them()
    {
        var assembly = typeof(TUnitRuntimeSessionTests).Assembly;
        using var session = new TUnitRuntimeSession(assembly, assembly.Location, "gen");
        var response = session.Run(
            CreateRequest(TestSelection.FromNames(["DoesNotExist"])),
            NullSink.Instance,
            TestContext.CancellationToken);

        Assert.AreEqual("testing/invalid_request", response.DiagnosticCode);
        Assert.Contains("Names", response.DiagnosticMessage!, StringComparison.Ordinal);
        Assert.IsEmpty(response.Results);
        Assert.AreEqual(TestCancellationState.None, response.CancellationState);
    }

    [TestMethod]
    public void Framework_filter_returns_invalid_request_without_throwing()
    {
        var assembly = typeof(TUnitRuntimeSessionTests).Assembly;
        using var session = new TUnitRuntimeSession(assembly, assembly.Location, "gen");
        var response = session.Run(
            CreateRequest(TestSelection.FromFrameworkFilter("filter-xml", "<filter/>")),
            NullSink.Instance,
            TestContext.CancellationToken);

        Assert.AreEqual("testing/invalid_request", response.DiagnosticCode);
        Assert.Contains("All or TestIds", response.DiagnosticMessage!, StringComparison.Ordinal);
        Assert.IsEmpty(response.Results);
        Assert.AreEqual(TestCancellationState.None, response.CancellationState);
    }

    [TestMethod]
    public void Cancel_before_active_run_id_is_applied_when_run_starts()
    {
        var assembly = typeof(TUnitRuntimeSessionTests).Assembly;
        using var session = new TUnitRuntimeSession(assembly, assembly.Location, "gen");
        var runId = Guid.NewGuid();
        session.Cancel(runId);

        var response = session.Run(
            CreateRequest(TestSelection.FromTestIds([]), runId),
            NullSink.Instance,
            TestContext.CancellationToken);

        Assert.AreEqual(TestCancellationState.Completed, response.CancellationState);
        Assert.IsEmpty(response.Results);
    }

    [TestMethod]
    public void Cancel_for_a_different_run_does_not_pending_cancel_the_next_run()
    {
        var assembly = typeof(TUnitRuntimeSessionTests).Assembly;
        using var session = new TUnitRuntimeSession(assembly, assembly.Location, "gen");
        session.Cancel(Guid.NewGuid());

        var response = session.Run(
            CreateRequest(TestSelection.FromTestIds([])),
            NullSink.Instance,
            TestContext.CancellationToken);

        Assert.AreEqual(TestCancellationState.None, response.CancellationState);
    }

    private static TestRunRequest CreateRequest(TestSelection selection, Guid? runId = null) =>
        new(
            2,
            runId ?? Guid.NewGuid(),
            TestFrameworkId.TUnit,
            new TestAssemblyReference(typeof(TUnitRuntimeSessionTests).Assembly.Location),
            selection);

    private sealed class NullSink : ITestEventSink
    {
        public static NullSink Instance { get; } = new();

        public void Publish(TestEvent testingEvent)
        {
        }
    }
}
