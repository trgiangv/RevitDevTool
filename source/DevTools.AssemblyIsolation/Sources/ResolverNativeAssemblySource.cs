#if NET
using System.Runtime.Loader;

namespace DevTools.AssemblyIsolation.Sources;

public sealed class ResolverNativeAssemblySource : INativeAssemblySource
{
    readonly AssemblyDependencyResolver resolver;
    readonly string root;

    public ResolverNativeAssemblySource(string entryAssemblyPath)
    {
        if (string.IsNullOrWhiteSpace(entryAssemblyPath))
            throw new ArgumentException("An entry assembly path is required.", nameof(entryAssemblyPath));

        var entry = Path.GetFullPath(entryAssemblyPath);
        root = Path.GetDirectoryName(entry)
            ?? throw new ArgumentException("The entry assembly path must have a directory.", nameof(entryAssemblyPath));
        resolver = new AssemblyDependencyResolver(entry);
    }

    public AssemblyCandidate? Resolve(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var path = resolver.ResolveUnmanagedDllToPath(name);
        return path is null ? null : AssemblyCandidate.TryCreate(path, root);
    }
}
#endif
