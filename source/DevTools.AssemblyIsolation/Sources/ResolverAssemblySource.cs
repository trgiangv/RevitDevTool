#if NET
using System.Reflection;
using System.Runtime.Loader;

namespace DevTools.AssemblyIsolation.Sources;

public sealed class ResolverAssemblySource : IManagedAssemblySource
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly string _root;

    public ResolverAssemblySource(string entryAssemblyPath)
    {
        if (string.IsNullOrWhiteSpace(entryAssemblyPath))
            throw new ArgumentException("An entry assembly path is required.", nameof(entryAssemblyPath));

        var entry = Path.GetFullPath(entryAssemblyPath);
        _root = Path.GetDirectoryName(entry)
            ?? throw new ArgumentException("The entry assembly path must have a directory.", nameof(entryAssemblyPath));
        _resolver = new AssemblyDependencyResolver(entry);
    }

    public AssemblyCandidate? Resolve(AssemblyName requested)
    {
        ArgumentNullException.ThrowIfNull(requested);

        var path = _resolver.ResolveAssemblyToPath(requested);
        return path is null ? null : AssemblyCandidate.TryCreate(path, _root);
    }
}
#endif
