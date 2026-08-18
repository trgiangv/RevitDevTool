using System.Reflection;
using System.Reflection.Emit;
using DevTools.AssemblyIsolation.Diagnostics;
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
            .WithLifecycle(AssemblyIsolationLifecycle.Collectible)
            .BindToParent(typeof(AssemblyIsolationPlanTests).Assembly)
            .AddManagedSource(managedSource)
            .AddNativeSource(nativeSource)
            .WithDiagnosticSink(sink);

        Assert.NotSame(initial, composed);
        Assert.Equal(AssemblyIsolationLifecycle.Permanent, initial.Lifecycle);
        Assert.Empty(initial.ManagedSources);
        Assert.Empty(initial.NativeSources);
        Assert.Null(initial.DiagnosticSink);
        Assert.False(initial.ParentBindings.TryResolve(typeof(AssemblyIsolationPlanTests).Assembly.GetName(), out _));

        Assert.Equal(AssemblyIsolationLifecycle.Collectible, composed.Lifecycle);
        Assert.Single(composed.ManagedSources);
        Assert.Single(composed.NativeSources);
        Assert.Same(sink, composed.DiagnosticSink);
        Assert.True(composed.ParentBindings.TryResolve(typeof(AssemblyIsolationPlanTests).Assembly.GetName(), out var parent));
        Assert.Same(typeof(AssemblyIsolationPlanTests).Assembly, parent);
    }

    [Fact]
    public void Plan_construction_rejects_incompatible_duplicate_parent_bindings()
    {
        var first = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("Duplicate.Plan.Binding") { Version = new Version(1, 0, 0, 0) },
            AssemblyBuilderAccess.Run);
        var second = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("Duplicate.Plan.Binding") { Version = new Version(2, 0, 0, 0) },
            AssemblyBuilderAccess.Run);

        var plan = AssemblyIsolationPlan.Create("entry.dll").BindToParent(first);

        Assert.Throws<DevTools.AssemblyIsolation.Identity.AssemblyIdentityMismatchException>(
            () => plan.BindToParent(second));
    }

    sealed class StubManagedSource : IManagedAssemblySource
    {
        public AssemblyCandidate? Resolve(AssemblyName requested) => null;
    }

    sealed class StubNativeSource : INativeAssemblySource
    {
        public AssemblyCandidate? Resolve(string unmanagedDllName) => null;
    }

    sealed class StubDiagnosticSink : IAssemblyIsolationDiagnosticSink
    {
        public void Publish(AssemblyIsolationDiagnostic diagnostic) { }
    }
}
