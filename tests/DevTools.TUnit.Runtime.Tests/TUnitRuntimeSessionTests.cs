using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Runtime;

namespace DevTools.TUnit.Runtime.Tests;

[Collection(nameof(TUnitSourceCatalogTests))]
public sealed class TUnitRuntimeSessionTests
{
    [Fact]
    public void Names_map_to_test_ids_and_do_not_throw()
    {
        var assembly = typeof(TUnitRuntimeSessionTests).Assembly;
        using var session = new TUnitRuntimeSession(assembly, assembly.Location, "gen");
        var response = session.Run(
            CreateRequest(TestingSelection.FromNames(["DoesNotExist"])),
            NullSink.Instance,
            TestContext.Current.CancellationToken);

        Assert.Null(response.DiagnosticCode);
        Assert.Empty(response.Results);
        Assert.Equal(TestingCancellationState.None, response.CancellationState);
    }

    [Fact]
    public void Framework_filter_returns_invalid_request_without_throwing()
    {
        var assembly = typeof(TUnitRuntimeSessionTests).Assembly;
        using var session = new TUnitRuntimeSession(assembly, assembly.Location, "gen");
        var response = session.Run(
            CreateRequest(TestingSelection.FromFrameworkFilter(TestingSelection.XmlFilterFormat, "<filter/>")),
            NullSink.Instance,
            TestContext.Current.CancellationToken);

        Assert.Equal("testing/invalid_request", response.DiagnosticCode);
        Assert.Contains("--name", response.DiagnosticMessage, StringComparison.Ordinal);
        Assert.Empty(response.Results);
        Assert.Equal(TestingCancellationState.None, response.CancellationState);
    }

    [Fact]
    public void Cancel_before_active_run_id_is_applied_when_run_starts()
    {
        var assembly = typeof(TUnitRuntimeSessionTests).Assembly;
        using var session = new TUnitRuntimeSession(assembly, assembly.Location, "gen");
        var runId = Guid.NewGuid();
        session.Cancel(runId);

        var response = session.Run(
            CreateRequest(TestingSelection.FromTestIds([]), runId),
            NullSink.Instance,
            TestContext.Current.CancellationToken);

        Assert.Equal(TestingCancellationState.Completed, response.CancellationState);
        Assert.Empty(response.Results);
    }

    [Fact]
    public void Cancel_for_a_different_run_does_not_pending_cancel_the_next_run()
    {
        var assembly = typeof(TUnitRuntimeSessionTests).Assembly;
        using var session = new TUnitRuntimeSession(assembly, assembly.Location, "gen");
        session.Cancel(Guid.NewGuid());

        var response = session.Run(
            CreateRequest(TestingSelection.FromTestIds([])),
            NullSink.Instance,
            TestContext.Current.CancellationToken);

        Assert.Equal(TestingCancellationState.None, response.CancellationState);
    }

    [Fact]
    public void MapToEngineSelection_converts_names_to_test_ids()
    {
        var assembly = typeof(TUnitRuntimeSessionTests).Assembly;
        var mapped = TUnitRuntimeSession.MapToEngineSelection(
            TestingSelection.FromNames(["DoesNotExist"]),
            assembly.Location,
            assembly);

        Assert.Equal(TestingSelectionKind.TestIds, mapped.Kind);
        Assert.Empty(mapped.TestIds);
    }

    private static TestingRunRequest CreateRequest(TestingSelection selection, Guid? runId = null) =>
        new(
            2,
            runId ?? Guid.NewGuid(),
            "tunit",
            new TestingAssemblyReference(typeof(TUnitRuntimeSessionTests).Assembly.Location),
            selection);

    private sealed class NullSink : ITestingRuntimeEventSink
    {
        public static NullSink Instance { get; } = new();

        public void Publish(TestingEvent testingEvent)
        {
        }
    }
}
