using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Providers;
using DevTools.Testing.Host;

namespace DevTools.Testing.Host.Tests;

public sealed class ProviderRegistryTests
{
    [Fact]
    public void GetRequired_is_case_stable()
    {
        var provider = new FakeProvider(TestingFrameworkIds.NUnit);
        var registry = new TestingProviderRegistry([provider]);

        Assert.Same(provider, registry.GetRequired("NUnit"));
        Assert.Same(provider, registry.GetRequired("nunit"));
    }

    [Fact]
    public void Constructor_rejects_duplicate_ids()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new TestingProviderRegistry(
            [
                new FakeProvider(TestingFrameworkIds.NUnit),
                new FakeProvider("NUnit"),
            ]));

        Assert.Contains("Duplicate", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetRequired_unknown_id_throws()
    {
        var registry = new TestingProviderRegistry([new FakeProvider(TestingFrameworkIds.NUnit)]);
        Assert.Throws<KeyNotFoundException>(() => registry.GetRequired("unregistered"));
    }

    [Fact]
    public void Cancel_notifies_every_registered_provider_without_framework_hardcoding()
    {
        var runId = Guid.NewGuid();
        var observed = new List<(string FrameworkId, Guid RunId)>();
        var first = new FakeProvider(TestingFrameworkIds.NUnit)
        {
            OnCancel = id =>
            {
                observed.Add((TestingFrameworkIds.NUnit, id));
                return false;
            },
        };
        var second = new FakeProvider("future-provider")
        {
            OnCancel = id =>
            {
                observed.Add(("future-provider", id));
                return true;
            },
        };
        var registry = new TestingProviderRegistry([first, second]);

        var acknowledged = registry.Cancel(runId);

        Assert.True(acknowledged);
        Assert.Equal(
            [(TestingFrameworkIds.NUnit, runId), ("future-provider", runId)],
            observed);
    }
}

internal sealed class FakeProvider(string frameworkId) : IHostTestFrameworkProvider
{
    public string FrameworkId { get; } = frameworkId;

    public Func<TestingRunRequest, TestingRunResponse>? OnRun { get; set; }

    public Func<Guid, bool>? OnCancel { get; set; }

    public Exception? RunException { get; set; }

    public TestingRunResponse Run(
        TestingRunRequest request,
        ITestingEventSink eventSink,
        CancellationToken cancellationToken)
    {
        if (RunException is not null)
            throw RunException;

        return OnRun?.Invoke(request)
            ?? new TestingRunResponse(
                request.RunId,
                FrameworkId,
                GenerationId: "gen",
                Results: [],
                CancellationState: TestingCancellationState.None,
                DiagnosticCode: null,
                DiagnosticMessage: null);
    }

    public bool Cancel(Guid runId) => OnCancel?.Invoke(runId) ?? false;
}
