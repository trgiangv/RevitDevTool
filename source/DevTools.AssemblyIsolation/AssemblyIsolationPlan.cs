using System.Reflection;
using DevTools.AssemblyIsolation.Diagnostics;
using DevTools.AssemblyIsolation.Identity;
using DevTools.AssemblyIsolation.Sources;

namespace DevTools.AssemblyIsolation;

public sealed class AssemblyIsolationPlan
{
    private readonly IReadOnlyList<SharedAssembly> sharedAssemblies;
    private readonly Dictionary<string, SharedAssembly> shares;

    private AssemblyIsolationPlan(
        string entryAssemblyPath,
        AssemblyIsolationKind kind,
        IReadOnlyList<SharedAssembly> sharedAssemblies,
        IReadOnlyList<IManagedAssemblySource> managedSources,
        IReadOnlyList<INativeAssemblySource> nativeSources,
        IAssemblyIsolationDiagnosticSink? diagnosticSink,
        bool loadsFromDistinctFile)
    {
        EntryAssemblyPath = entryAssemblyPath;
        Kind = kind;
        this.sharedAssemblies = sharedAssemblies;
        shares = Index(sharedAssemblies);
        ManagedSources = managedSources;
        NativeSources = nativeSources;
        DiagnosticSink = diagnosticSink;
        LoadsFromDistinctFile = loadsFromDistinctFile;
    }

    public string EntryAssemblyPath { get; }

    public AssemblyIsolationKind Kind { get; }

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
            AssemblyIsolationKind.Permanent,
            ReadOnly(Array.Empty<SharedAssembly>()),
            ReadOnly(Array.Empty<IManagedAssemblySource>()),
            ReadOnly(Array.Empty<INativeAssemblySource>()),
            null,
            false);
    }

    public AssemblyIsolationPlan WithKind(AssemblyIsolationKind kind) =>
        Clone(kind: kind);

    public AssemblyIsolationPlan WithDistinctFileIdentity() =>
        Clone(loadsFromDistinctFile: true);

    /// <summary>
    /// Reuse this loaded assembly. Version may differ; name, culture, and token must match.
    /// </summary>
    public AssemblyIsolationPlan Share(Assembly assembly) => AddShare(assembly, allowVersionDrift: true);

    /// <summary>
    /// Reuse this loaded assembly only when the full identity matches.
    /// </summary>
    public AssemblyIsolationPlan Pin(Assembly assembly) => AddShare(assembly, allowVersionDrift: false);

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

    public bool TryShare(AssemblyName requested, out Assembly assembly)
    {
        if (requested is null) throw new ArgumentNullException(nameof(requested));

        if (requested.Name is null || !shares.TryGetValue(requested.Name, out var shared))
        {
            assembly = null!;
            return false;
        }

        if (!AssemblyIdentityMatcher.IsCompatible(requested, shared.Identity, shared.AllowVersionDrift))
            throw new AssemblyIdentityMismatchException(requested, shared.Identity);

        if (shared.AllowVersionDrift
            && requested.Version is not null
            && requested.Version != shared.Identity.Version)
        {
            DiagnosticSink?.Publish(new AssemblyIsolationDiagnostic(
                "share-version-drift",
                $"Requested '{requested.FullName}' shares loaded '{shared.Identity.FullName}'.",
                requested));
        }

        assembly = shared.Assembly;
        return true;
    }

    private AssemblyIsolationPlan AddShare(Assembly assembly, bool allowVersionDrift)
    {
        if (assembly is null) throw new ArgumentNullException(nameof(assembly));

        return Clone(sharedAssemblies: ReadOnly(AppendShare(
            sharedAssemblies,
            new SharedAssembly(assembly, allowVersionDrift))));
    }

    private AssemblyIsolationPlan Clone(
        AssemblyIsolationKind? kind = null,
        IReadOnlyList<SharedAssembly>? sharedAssemblies = null,
        IReadOnlyList<IManagedAssemblySource>? managedSources = null,
        IReadOnlyList<INativeAssemblySource>? nativeSources = null,
        IAssemblyIsolationDiagnosticSink? diagnosticSink = null,
        bool? loadsFromDistinctFile = null) =>
        new(
            EntryAssemblyPath,
            kind ?? Kind,
            sharedAssemblies ?? this.sharedAssemblies,
            managedSources ?? ManagedSources,
            nativeSources ?? NativeSources,
            diagnosticSink ?? DiagnosticSink,
            loadsFromDistinctFile ?? LoadsFromDistinctFile);

    private static Dictionary<string, SharedAssembly> Index(IReadOnlyList<SharedAssembly> items)
    {
        var map = new Dictionary<string, SharedAssembly>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
            map[item.SimpleName] = item;
        return map;
    }

    private static IEnumerable<SharedAssembly> AppendShare(
        IReadOnlyList<SharedAssembly> existing,
        SharedAssembly next)
    {
        foreach (var item in existing)
        {
            if (!string.Equals(item.SimpleName, next.SimpleName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (ReferenceEquals(item.Assembly, next.Assembly)
                && item.AllowVersionDrift == next.AllowVersionDrift)
                return existing;

            throw new AssemblyIdentityMismatchException(next.Identity, item.Identity);
        }

        return existing.Append(next);
    }

    private static IReadOnlyList<T> ReadOnly<T>(IEnumerable<T> items) =>
        Array.AsReadOnly(items.ToArray());

    private readonly struct SharedAssembly
    {
        public SharedAssembly(Assembly assembly, bool allowVersionDrift)
        {
            Assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
            Identity = assembly.GetName();
            SimpleName = Identity.Name
                ?? throw new ArgumentException("The assembly must have a simple name.", nameof(assembly));
            AllowVersionDrift = allowVersionDrift;
        }

        public Assembly Assembly { get; }

        public AssemblyName Identity { get; }

        public string SimpleName { get; }

        public bool AllowVersionDrift { get; }
    }
}
