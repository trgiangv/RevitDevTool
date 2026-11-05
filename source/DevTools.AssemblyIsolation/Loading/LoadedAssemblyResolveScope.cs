using System.Reflection;

namespace DevTools.AssemblyIsolation.Loading;

/// <summary>
/// While open, answers <see cref="AppDomain.AssemblyResolve"/> with one of the
/// given loaded assemblies when the full identity matches. Never loads from disk;
/// a different version is not a substitute.
/// </summary>
public sealed class LoadedAssemblyResolveScope : IDisposable
{
    private readonly Dictionary<string, Assembly> _byFullName = new(StringComparer.Ordinal);
    private bool _disposed;

    public LoadedAssemblyResolveScope(IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        foreach (var assembly in assemblies)
        {
            if (assembly.IsDynamic || assembly.FullName is not { Length: > 0 } fullName)
                continue;
            _byFullName.TryAdd(fullName, assembly);
        }

        AppDomain.CurrentDomain.AssemblyResolve += Resolve;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        AppDomain.CurrentDomain.AssemblyResolve -= Resolve;
    }

    internal Assembly? ResolveForTesting(AssemblyName requested) => Find(requested);

    private Assembly? Resolve(object? sender, ResolveEventArgs args) => Find(new AssemblyName(args.Name));

    private Assembly? Find(AssemblyName requested) =>
        _byFullName.GetValueOrDefault(requested.FullName);
}
