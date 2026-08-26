#if NETFRAMEWORK
using System.Reflection;
using System.Runtime.CompilerServices;
using DevTools.AssemblyIsolation.Diagnostics;
using DevTools.AssemblyIsolation.Identity;
using DevTools.AssemblyIsolation.Loading;
using DevTools.AssemblyIsolation.Sources;

namespace DevTools.AssemblyIsolation.Runtime;

internal sealed class NetfxAssemblyIsolationContext : IDisposable
{
    readonly AssemblyIsolationPlan plan;
    readonly ResolveEventHandler resolver;
    readonly HashSet<Assembly> ownedAssemblies = new(ReferenceEqualityComparer.Instance);
    int activeLoads;
    bool disposed;

    public NetfxAssemblyIsolationContext(AssemblyIsolationPlan plan)
    {
        this.plan = plan ?? throw new ArgumentNullException(nameof(plan));
        resolver = Resolve;
        AppDomain.CurrentDomain.AssemblyResolve += resolver;
    }

    public Assembly LoadEntryAssembly()
    {
        ThrowIfDisposed();
        var requested = AssemblyName.GetAssemblyName(plan.EntryAssemblyPath);
        if (plan.TryShare(requested, out var parent))
            return parent;

        using (BeginLoad())
            return Own(LoadManaged(plan.EntryAssemblyPath));
    }

    public Assembly LoadFromPath(string path)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("An assembly path is required.", nameof(path));

        using (BeginLoad())
            return Own(LoadManaged(path));
    }

    public Assembly LoadAssembly(byte[] assemblyBytes)
    {
        ThrowIfDisposed();
        if (assemblyBytes is null) throw new ArgumentNullException(nameof(assemblyBytes));

        using (BeginLoad())
            return Own(Assembly.Load(assemblyBytes));
    }

    public void Dispose()
    {
        if (disposed)
            return;

        AppDomain.CurrentDomain.AssemblyResolve -= resolver;
        disposed = true;
    }

    Assembly? Resolve(object? sender, ResolveEventArgs args)
    {
        if (disposed)
            return null;

        if (!ShouldServe(args.RequestingAssembly))
            return null;

        var requested = new AssemblyName(args.Name);
        if (plan.TryShare(requested, out var parent))
            return parent;

        foreach (var source in plan.ManagedSources)
        {
            var candidate = source.Resolve(requested);
            if (candidate is null)
                continue;

            if (!TryValidateCandidate(requested, candidate, out var rejection))
            {
                Publish("managed-candidate-rejected", requested, candidate, rejection!);
                continue;
            }

            Publish("managed-candidate-selected", requested, candidate, "Loading private candidate.");
            return Own(LoadManaged(candidate.Path));
        }

        return null;
    }

    bool ShouldServe(Assembly? requestingAssembly)
    {
        if (Volatile.Read(ref activeLoads) > 0)
            return true;

        return requestingAssembly is not null && ownedAssemblies.Contains(requestingAssembly);
    }

    Assembly LoadManaged(string path) =>
        plan.LoadsFromDistinctFile
            ? AssemblyStreamLoader.LoadFile(path)
            : AssemblyStreamLoader.Load(path);

    LoadGuard BeginLoad() => new(this);

    Assembly Own(Assembly assembly)
    {
        ownedAssemblies.Add(assembly);
        return assembly;
    }

    static bool TryValidateCandidate(AssemblyName requested, AssemblyCandidate candidate, out string? rejection)
    {
        if (!File.Exists(candidate.Path))
        {
            rejection = "Candidate file does not exist.";
            return false;
        }

        if (!AssemblyCandidate.IsExistingPathUnderRoot(candidate.Path, candidate.Root))
        {
            rejection = "Candidate is outside its root.";
            return false;
        }

        var candidateIdentity = AssemblyName.GetAssemblyName(candidate.Path);
        if (!AssemblyIdentityMatcher.IsCompatible(requested, candidateIdentity))
        {
            rejection = $"Candidate identity '{candidateIdentity.FullName}' is incompatible.";
            return false;
        }

        rejection = null;
        return true;
    }

    void Publish(string code, AssemblyName? requested, AssemblyCandidate? candidate, string reason)
    {
        var detail = candidate is null ? "" : $", candidate '{candidate.Path}'";
        var identity = requested?.FullName ?? "native library";
        plan.DiagnosticSink?.Publish(new AssemblyIsolationDiagnostic(code, $"Requested '{identity}'{detail}: {reason}", requested));
    }

    void ThrowIfDisposed()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(NetfxAssemblyIsolationContext));
    }

    sealed class LoadGuard : IDisposable
    {
        readonly NetfxAssemblyIsolationContext owner;

        public LoadGuard(NetfxAssemblyIsolationContext owner)
        {
            this.owner = owner;
            Interlocked.Increment(ref owner.activeLoads);
        }

        public void Dispose() => Interlocked.Decrement(ref owner.activeLoads);
    }

    sealed class ReferenceEqualityComparer : IEqualityComparer<Assembly>
    {
        public static ReferenceEqualityComparer Instance { get; } = new();

        public bool Equals(Assembly? x, Assembly? y) => ReferenceEquals(x, y);

        public int GetHashCode(Assembly obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
#endif

