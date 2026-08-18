using System.Reflection;
using DevTools.AssemblyIsolation.Bindings;
using DevTools.AssemblyIsolation.Diagnostics;
using DevTools.AssemblyIsolation.Sources;

namespace DevTools.AssemblyIsolation;

public sealed class AssemblyIsolationPlan
{
    readonly IReadOnlyList<Assembly> parentAssemblies;

    AssemblyIsolationPlan(
        string entryAssemblyPath,
        AssemblyIsolationLifecycle lifecycle,
        IReadOnlyList<Assembly> parentAssemblies,
        IReadOnlyList<IManagedAssemblySource> managedSources,
        IReadOnlyList<INativeAssemblySource> nativeSources,
        IAssemblyIsolationDiagnosticSink? diagnosticSink)
    {
        EntryAssemblyPath = entryAssemblyPath;
        Lifecycle = lifecycle;
        this.parentAssemblies = parentAssemblies;
        ParentBindings = ParentAssemblyBindings.Create(parentAssemblies);
        ManagedSources = managedSources;
        NativeSources = nativeSources;
        DiagnosticSink = diagnosticSink;
    }

    public string EntryAssemblyPath { get; }

    public AssemblyIsolationLifecycle Lifecycle { get; }

    public ParentAssemblyBindings ParentBindings { get; }

    public IReadOnlyList<IManagedAssemblySource> ManagedSources { get; }

    public IReadOnlyList<INativeAssemblySource> NativeSources { get; }

    public IAssemblyIsolationDiagnosticSink? DiagnosticSink { get; }

    public static AssemblyIsolationPlan Create(string entryAssemblyPath)
    {
        if (string.IsNullOrWhiteSpace(entryAssemblyPath))
            throw new ArgumentException("An entry assembly path is required.", nameof(entryAssemblyPath));

        return new AssemblyIsolationPlan(
            Path.GetFullPath(entryAssemblyPath),
            AssemblyIsolationLifecycle.Permanent,
            ReadOnly(Array.Empty<Assembly>()),
            ReadOnly(Array.Empty<IManagedAssemblySource>()),
            ReadOnly(Array.Empty<INativeAssemblySource>()),
            null);
    }

    public AssemblyIsolationPlan WithLifecycle(AssemblyIsolationLifecycle lifecycle) =>
        new(
            EntryAssemblyPath,
            lifecycle,
            parentAssemblies,
            ManagedSources,
            NativeSources,
            DiagnosticSink);

    public AssemblyIsolationPlan BindToParent(Assembly assembly)
    {
        if (assembly is null) throw new ArgumentNullException(nameof(assembly));

        return new AssemblyIsolationPlan(
            EntryAssemblyPath,
            Lifecycle,
            ReadOnly(parentAssemblies.Append(assembly)),
            ManagedSources,
            NativeSources,
            DiagnosticSink);
    }

    public AssemblyIsolationPlan AddManagedSource(IManagedAssemblySource source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        return new AssemblyIsolationPlan(
            EntryAssemblyPath,
            Lifecycle,
            parentAssemblies,
            ReadOnly(ManagedSources.Append(source)),
            NativeSources,
            DiagnosticSink);
    }

    public AssemblyIsolationPlan AddNativeSource(INativeAssemblySource source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        return new AssemblyIsolationPlan(
            EntryAssemblyPath,
            Lifecycle,
            parentAssemblies,
            ManagedSources,
            ReadOnly(NativeSources.Append(source)),
            DiagnosticSink);
    }

    public AssemblyIsolationPlan WithDiagnosticSink(IAssemblyIsolationDiagnosticSink sink)
    {
        if (sink is null) throw new ArgumentNullException(nameof(sink));

        return new AssemblyIsolationPlan(
            EntryAssemblyPath,
            Lifecycle,
            parentAssemblies,
            ManagedSources,
            NativeSources,
            sink);
    }

    static IReadOnlyList<T> ReadOnly<T>(IEnumerable<T> items) =>
        Array.AsReadOnly(items.ToArray());
}
