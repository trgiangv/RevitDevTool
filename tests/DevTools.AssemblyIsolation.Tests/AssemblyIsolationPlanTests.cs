using System.Reflection;
using System.Reflection.Emit;
using DevTools.AssemblyIsolation.Diagnostics;
using DevTools.AssemblyIsolation.Identity;
using DevTools.AssemblyIsolation.Sources;

namespace DevTools.AssemblyIsolation.Tests;

[TestClass]
public sealed class AssemblyIsolationPlanTests
{
    [TestMethod]
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

        Assert.AreNotSame(initial, composed);
        Assert.IsFalse(initial.LoadsFromDistinctFile);
        Assert.IsTrue(composed.LoadsFromDistinctFile);
        Assert.AreEqual(AssemblyIsolationKind.Permanent, initial.Kind);
        Assert.IsEmpty(initial.ManagedSources);
        Assert.IsEmpty(initial.NativeSources);
        Assert.IsNull(initial.DiagnosticSink);
        Assert.IsFalse(initial.TryShare(typeof(AssemblyIsolationPlanTests).Assembly.GetName(), out _));

        Assert.AreEqual(AssemblyIsolationKind.Isolated, composed.Kind);
        Assert.AreEqual(1, composed.ManagedSources.Count);
        Assert.AreEqual(1, composed.NativeSources.Count);
        Assert.AreSame(sink, composed.DiagnosticSink);
        Assert.IsTrue(composed.TryShare(typeof(AssemblyIsolationPlanTests).Assembly.GetName(), out var parent));
        Assert.AreSame(typeof(AssemblyIsolationPlanTests).Assembly, parent);
    }

    [TestMethod]
    public void Plan_construction_rejects_incompatible_duplicate_shares()
    {
        var first = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("Duplicate.Plan.Binding") { Version = new Version(1, 0, 0, 0) },
            AssemblyBuilderAccess.Run);
        var second = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("Duplicate.Plan.Binding") { Version = new Version(2, 0, 0, 0) },
            AssemblyBuilderAccess.Run);

        var plan = AssemblyIsolationPlan.Create("entry.dll").Share(first);

        Assert.ThrowsExactly<AssemblyMismatchException>(() => plan.Share(second));
    }

    [TestMethod]
    public void Pin_rejects_requested_version_drift()
    {
        var loaded = typeof(AssemblyIsolationPlanTests).Assembly;
        var requested = new AssemblyName(loaded.FullName!) { Version = new Version(99, 0, 0, 0) };
        var plan = AssemblyIsolationPlan.Create("entry.dll").Pin(loaded);

        var error = Assert.ThrowsExactly<AssemblyMismatchException>(
            () => plan.TryShare(requested, out _));
        Assert.Contains(loaded.GetName().Name!, error.Message, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Share_accepts_requested_version_drift_without_publishing()
    {
        var loaded = typeof(AssemblyIsolationPlanTests).Assembly;
        var requested = new AssemblyName(loaded.FullName!) { Version = new Version(99, 0, 0, 0) };
        var sink = new StubDiagnosticSink();
        var plan = AssemblyIsolationPlan.Create("entry.dll")
            .Share(loaded)
            .WithDiagnosticSink(sink);

        Assert.IsTrue(plan.TryShare(requested, out var actual));
        Assert.AreSame(loaded, actual);
        Assert.IsEmpty(sink.Diagnostics);
    }

    [TestMethod]
    public void Share_collapses_the_same_instance()
    {
        var loaded = typeof(AssemblyIsolationPlanTests).Assembly;
        var plan = AssemblyIsolationPlan.Create("entry.dll").Share(loaded).Share(loaded);

        Assert.IsTrue(plan.TryShare(loaded.GetName(), out var actual));
        Assert.AreSame(loaded, actual);
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
