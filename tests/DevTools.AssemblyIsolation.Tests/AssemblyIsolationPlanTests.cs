using System.Reflection;
using System.Reflection.Emit;
using DevTools.AssemblyIsolation.Diagnostics;
using DevTools.AssemblyIsolation.Identity;
using DevTools.AssemblyIsolation.Sources;

namespace DevTools.AssemblyIsolation.Tests;

public sealed class AssemblyIsolationPlanTests
{
    [Fact]
    public void Plan_composition_returns_new_instances_without_mutating_the_prior_plan()
    {
        var initial = AssemblyIsolationPlan.Create("entry.dll");
        var managedSource = new StubManagedSource();
        var nativeSource = new StubNativeSource();
        var sink = new StubDiagnosticSink();

        var composed = initial
            .WithKind(AssemblyIsolationKind.Isolated)
            .WithDistinctFileIdentity()
            .Pin(typeof(AssemblyIsolationPlanTests).Assembly)
            .AddManagedSource(managedSource)
            .AddNativeSource(nativeSource)
            .WithDiagnosticSink(sink);

        Assert.NotSame(initial, composed);
        Assert.False(initial.LoadsFromDistinctFile);
        Assert.True(composed.LoadsFromDistinctFile);
        Assert.Equal(AssemblyIsolationKind.Permanent, initial.Kind);
        Assert.Empty(initial.ManagedSources);
        Assert.Empty(initial.NativeSources);
        Assert.Null(initial.DiagnosticSink);
        Assert.False(initial.TryShare(typeof(AssemblyIsolationPlanTests).Assembly.GetName(), out _));

        Assert.Equal(AssemblyIsolationKind.Isolated, composed.Kind);
        Assert.Single(composed.ManagedSources);
        Assert.Single(composed.NativeSources);
        Assert.Same(sink, composed.DiagnosticSink);
        Assert.True(composed.TryShare(typeof(AssemblyIsolationPlanTests).Assembly.GetName(), out var parent));
        Assert.Same(typeof(AssemblyIsolationPlanTests).Assembly, parent);
    }

    [Fact]
    public void Plan_construction_rejects_incompatible_duplicate_shares()
    {
        var first = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("Duplicate.Plan.Binding") { Version = new Version(1, 0, 0, 0) },
            AssemblyBuilderAccess.Run);
        var second = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("Duplicate.Plan.Binding") { Version = new Version(2, 0, 0, 0) },
            AssemblyBuilderAccess.Run);

        var plan = AssemblyIsolationPlan.Create("entry.dll").Share(first);

        Assert.Throws<AssemblyMismatchException>(() => plan.Share(second));
    }

    [Fact]
    public void Pin_rejects_requested_version_drift()
    {
        var loaded = typeof(AssemblyIsolationPlanTests).Assembly;
        var requested = new AssemblyName(loaded.FullName!) { Version = new Version(99, 0, 0, 0) };
        var plan = AssemblyIsolationPlan.Create("entry.dll").Pin(loaded);

        var error = Assert.Throws<AssemblyMismatchException>(
            () => plan.TryShare(requested, out _));
        Assert.Contains(loaded.GetName().Name!, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Share_accepts_requested_version_drift_without_publishing()
    {
        var loaded = typeof(AssemblyIsolationPlanTests).Assembly;
        var requested = new AssemblyName(loaded.FullName!) { Version = new Version(99, 0, 0, 0) };
        var sink = new StubDiagnosticSink();
        var plan = AssemblyIsolationPlan.Create("entry.dll")
            .Share(loaded)
            .WithDiagnosticSink(sink);

        Assert.True(plan.TryShare(requested, out var actual));
        Assert.Same(loaded, actual);
        Assert.Empty(sink.Diagnostics);
    }

    [Fact]
    public void Share_collapses_the_same_instance()
    {
        var loaded = typeof(AssemblyIsolationPlanTests).Assembly;
        var plan = AssemblyIsolationPlan.Create("entry.dll").Share(loaded).Share(loaded);

        Assert.True(plan.TryShare(loaded.GetName(), out var actual));
        Assert.Same(loaded, actual);
    }

    sealed class StubManagedSource : IManagedAssemblySource
    {
        public AssemblyCandidate? Resolve(AssemblyName requested) => null;
    }

    sealed class StubNativeSource : INativeAssemblySource
    {
        public AssemblyCandidate? Resolve(string name) => null;
    }

    sealed class StubDiagnosticSink : IAssemblyIsolationDiagnosticSink
    {
        public List<AssemblyIsolationDiagnostic> Diagnostics { get; } = [];

        public void Publish(AssemblyIsolationDiagnostic diagnostic) => Diagnostics.Add(diagnostic);
    }
}
