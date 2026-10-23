#if NETFRAMEWORK
using DevTools.AssemblyIsolation.Diagnostics;
using DevTools.AssemblyIsolation.Identity;
using DevTools.AssemblyIsolation.Loading;
using DevTools.AssemblyIsolation.Sources;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DevTools.AssemblyIsolation.Runtime;

internal sealed class NetfxAssemblyIsolationContext : IDisposable
{
    private readonly AssemblyIsolationPlan _plan;
    private readonly ResolveEventHandler _resolver;
    private readonly HashSet<Assembly> _ownedAssemblies = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<string, Assembly> _loadedByPath = new(StringComparer.OrdinalIgnoreCase);
    private int _activeLoads;
    private bool _disposed;

    public NetfxAssemblyIsolationContext(AssemblyIsolationPlan plan)
    {
        _plan = plan ?? throw new ArgumentNullException(nameof(plan));
        _resolver = Resolve;
        AppDomainResolver.InsertFirst(AppDomain.CurrentDomain, _resolver);
    }

    public Assembly LoadEntryAssembly()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var requested = AssemblyName.GetAssemblyName(_plan.EntryAssemblyPath);
        if (_plan.TryShare(requested, out var parent))
            return parent;

        return Own(LoadManaged(_plan.EntryAssemblyPath));
    }

    public Assembly LoadFromPath(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("An assembly path is required.", nameof(path));

        return Own(LoadManaged(path));
    }

    public Assembly LoadAssembly(byte[] assemblyBytes, byte[]? symbolBytes = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(assemblyBytes);

        using (BeginLoad())
        {
            return Own(symbolBytes is { Length: > 0 }
                ? Assembly.Load(assemblyBytes, symbolBytes)
                : Assembly.Load(assemblyBytes));
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        AppDomainResolver.Remove(AppDomain.CurrentDomain, _resolver);
        _disposed = true;
    }

    private Assembly? Resolve(object? sender, ResolveEventArgs args)
    {
        if (_disposed)
            return null;

        var requested = new AssemblyName(args.Name);
        if (_plan.TryShare(requested, out var parent))
            return parent;

        if (!ShouldServe(args.RequestingAssembly))
            return null;

        if (NetfxClosureBind.TryFindLoaded(requested, SessionLoaded(), out var loaded))
            return Own(loaded);

        foreach (var source in _plan.ManagedSources)
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

    private bool ShouldServe(Assembly? requestingAssembly)
    {
        if (Volatile.Read(ref _activeLoads) > 0)
            return true;

        return requestingAssembly is not null && _ownedAssemblies.Contains(requestingAssembly);
    }

    private Assembly LoadManaged(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (_loadedByPath.TryGetValue(fullPath, out var loaded))
            return loaded;

        // LoadFile of A fires AssemblyResolve for A's refs before A is Own()'d.
        // Active loads keep ShouldServe true so those refs bind this session's
        // closure, not another add-in's copy already in DefaultDomain.
        using (BeginLoad())
        {
            if (_loadedByPath.TryGetValue(fullPath, out loaded))
                return loaded;

            var assembly = _plan.LoadsFromDistinctFile
                ? AssemblyStreamLoader.LoadFile(fullPath)
                : AssemblyStreamLoader.Load(fullPath);
            _loadedByPath[fullPath] = assembly;
            return assembly;
        }
    }

    private IEnumerable<Assembly> SessionLoaded() =>
        _ownedAssemblies.Concat(_loadedByPath.Values);

    private LoadGuard BeginLoad() => new(this);

    private Assembly Own(Assembly assembly)
    {
        _ownedAssemblies.Add(assembly);
        return assembly;
    }

    private static bool TryValidateCandidate(AssemblyName requested, AssemblyCandidate candidate, out string? rejection)
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
        if (!AssemblyIdentityMatcher.IsCompatible(requested, candidateIdentity)
            && !NetfxClosureBind.AllowsNewer(requested, candidateIdentity))
        {
            rejection = $"Candidate identity '{candidateIdentity.FullName}' is incompatible.";
            return false;
        }

        rejection = null;
        return true;
    }

    private void Publish(string code, AssemblyName? requested, AssemblyCandidate? candidate, string reason)
    {
        var detail = candidate is null ? "" : $", candidate '{candidate.Path}'";
        var identity = requested?.FullName ?? "native library";
        _plan.DiagnosticSink?.Publish(new AssemblyIsolationDiagnostic(code, $"Requested '{identity}'{detail}: {reason}", requested));
    }

    private sealed class LoadGuard : IDisposable
    {
        private readonly NetfxAssemblyIsolationContext _owner;

        public LoadGuard(NetfxAssemblyIsolationContext owner)
        {
            _owner = owner;
            Interlocked.Increment(ref _owner._activeLoads);
        }

        public void Dispose() => Interlocked.Decrement(ref _owner._activeLoads);
    }

    private sealed class ReferenceEqualityComparer : IEqualityComparer<Assembly>
    {
        public static ReferenceEqualityComparer Instance { get; } = new();

        public bool Equals(Assembly? x, Assembly? y) => ReferenceEquals(x, y);

        public int GetHashCode(Assembly obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
#endif

