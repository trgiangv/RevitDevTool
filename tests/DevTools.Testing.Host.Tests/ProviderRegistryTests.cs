using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Providers;
using DevTools.Testing.Host;

namespace DevTools.Testing.Host.Tests;

public sealed class ProviderRegistryTests
{
    [Fact]
    public void Constructor_rejects_duplicate_ids()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new TestingProviderRegistry(
            [
                new FakeProvider(TestFrameworkId.NUnit),
                new FakeProvider(TestFrameworkId.NUnit),
            ]));

        Assert.Contains("Duplicate", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetRequired_unknown_id_throws()
    {
        var registry = new TestingProviderRegistry([new FakeProvider(TestFrameworkId.NUnit)]);
        Assert.Throws<KeyNotFoundException>(() => registry.GetRequired(TestFrameworkId.TUnit));
    }

    [Fact]
    public void Cancel_notifies_every_registered_provider()
    {
        var runId = Guid.NewGuid();
        var observed = new List<(TestFrameworkId FrameworkId, Guid RunId)>();
        var first = new FakeProvider(TestFrameworkId.NUnit)
        {
            OnCancel = id =>
            {
                observed.Add((TestFrameworkId.NUnit, id));
                return false;
            },
        };
        var second = new FakeProvider(TestFrameworkId.TUnit)
        {
            OnCancel = id =>
            {
                observed.Add((TestFrameworkId.TUnit, id));
                return true;
            },
        };
        var registry = new TestingProviderRegistry([first, second]);

        var acknowledged = registry.Cancel(runId);

        Assert.True(acknowledged);
        Assert.Equal(
            [(TestFrameworkId.NUnit, runId), (TestFrameworkId.TUnit, runId)],
            observed);
    }
}

internal sealed class FakeProvider(TestFrameworkId frameworkId) : ITestFrameworkProvider
{
    public TestFrameworkId FrameworkId { get; } = frameworkId;

    public Func<TestRunRequest, TestRunResponse>? OnRun { get; set; }

    public Func<Guid, bool>? OnCancel { get; set; }

    public Exception? RunException { get; set; }

    public TestRunResponse Run(
        TestRunRequest request,
        ITestEventSink eventSink,
        CancellationToken cancellationToken)
    {
        if (RunException is not null)
            throw RunException;

        return OnRun?.Invoke(request)
            ?? new TestRunResponse(
                request.RunId,
                FrameworkId,
                GenerationId: "gen",
                Results: [],
                CancellationState: TestCancellationState.None,
                DiagnosticCode: null,
                DiagnosticMessage: null);
    }

    public bool Cancel(Guid runId) => OnCancel?.Invoke(runId) ?? false;
}
