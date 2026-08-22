using System.Reflection;
using DevTools.AssemblyIsolation.Bindings;
using DevTools.AssemblyIsolation.Diagnostics;
using DevTools.AssemblyIsolation.Sources;

namespace DevTools.AssemblyIsolation;

public sealed class AssemblyIsolationPlan
{
    private readonly IReadOnlyList<Assembly> parentAssemblies;

    AssemblyIsolationPlan(
        string entryAssemblyPath,
        AssemblyIsolationLifecycle lifecycle,
        IReadOnlyList<Assembly> parentAssemblies,
        IReadOnlyList<IManagedAssemblySource> managedSources,
        IReadOnlyList<INativeAssemblySource> nativeSources,
        IAssemblyIsolationDiagnosticSink? diagnosticSink,
        bool loadsFromDistinctFile)
    {
        EntryAssemblyPath = entryAssemblyPath;
        Lifecycle = lifecycle;
        this.parentAssemblies = parentAssemblies;
        ParentBindings = ParentAssemblyBindings.Create(parentAssemblies);
        ManagedSources = managedSources;
        NativeSources = nativeSources;
        DiagnosticSink = diagnosticSink;
        LoadsFromDistinctFile = loadsFromDistinctFile;
    }

    public string EntryAssemblyPath { get; }

    public AssemblyIsolationLifecycle Lifecycle { get; }

    public ParentAssemblyBindings ParentBindings { get; }

    public IReadOnlyList<IManagedAssemblySource> ManagedSources { get; }

    public IReadOnlyList<INativeAssemblySource> NativeSources { get; }

    public IAssemblyIsolationDiagnosticSink? DiagnosticSink { get; }

    /// <summary>
    /// When true, net48 uses path-backed load so the same assembly identity from
    /// different files stays distinct. Default is memory load (no source lock).
    /// </summary>
    public bool LoadsFromDistinctFile { get; }

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
            null,
            false);
    }

    public AssemblyIsolationPlan WithLifecycle(AssemblyIsolationLifecycle lifecycle) =>
        Clone(lifecycle: lifecycle);

    public AssemblyIsolationPlan WithDistinctFileIdentity() =>
        Clone(loadsFromDistinctFile: true);

    public AssemblyIsolationPlan BindToParent(Assembly assembly)
    {
        if (assembly is null) throw new ArgumentNullException(nameof(assembly));

        return Clone(parentAssemblies: ReadOnly(this.parentAssemblies.Append(assembly)));
    }

    public AssemblyIsolationPlan AddManagedSource(IManagedAssemblySource source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        return Clone(managedSources: ReadOnly(ManagedSources.Append(source)));
    }

    public AssemblyIsolationPlan AddNativeSource(INativeAssemblySource source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        return Clone(nativeSources: ReadOnly(NativeSources.Append(source)));
    }

    public AssemblyIsolationPlan WithDiagnosticSink(IAssemblyIsolationDiagnosticSink sink)
    {
        if (sink is null) throw new ArgumentNullException(nameof(sink));

        return Clone(diagnosticSink: sink);
    }

    private AssemblyIsolationPlan Clone(
        AssemblyIsolationLifecycle? lifecycle = null,
        IReadOnlyList<Assembly>? parentAssemblies = null,
        IReadOnlyList<IManagedAssemblySource>? managedSources = null,
        IReadOnlyList<INativeAssemblySource>? nativeSources = null,
        IAssemblyIsolationDiagnosticSink? diagnosticSink = null,
        bool? loadsFromDistinctFile = null) =>
        new(
            EntryAssemblyPath,
            lifecycle ?? Lifecycle,
            parentAssemblies ?? this.parentAssemblies,
            managedSources ?? ManagedSources,
            nativeSources ?? NativeSources,
            diagnosticSink ?? DiagnosticSink,
            loadsFromDistinctFile ?? LoadsFromDistinctFile);

    private static IReadOnlyList<T> ReadOnly<T>(IEnumerable<T> items) =>
        Array.AsReadOnly(items.ToArray());
}
