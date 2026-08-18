using System.Reflection;
#if NET
using System.Runtime.Loader;
#endif

namespace DevTools.AssemblyIsolation.Sources;

public sealed class DependencyResolverAssemblySource : IManagedAssemblySource
{
    readonly string sourceName;
    readonly string allowedRoot;
#if NET
    readonly AssemblyDependencyResolver resolver;
#endif

    public DependencyResolverAssemblySource(string entryAssemblyPath, string sourceName = "dependency resolver")
    {
        if (string.IsNullOrWhiteSpace(entryAssemblyPath))
            throw new ArgumentException("An entry assembly path is required.", nameof(entryAssemblyPath));
        if (string.IsNullOrWhiteSpace(sourceName))
            throw new ArgumentException("A source name is required.", nameof(sourceName));

        var normalizedEntryPath = Path.GetFullPath(entryAssemblyPath);
        allowedRoot = Path.GetDirectoryName(normalizedEntryPath)
            ?? throw new ArgumentException("The entry assembly path must have a directory.", nameof(entryAssemblyPath));
        this.sourceName = sourceName;
#if NET
        resolver = new AssemblyDependencyResolver(normalizedEntryPath);
#else
        throw new PlatformNotSupportedException("AssemblyDependencyResolver is available only on .NET.");
#endif
    }

    public AssemblyCandidate? Resolve(AssemblyName requested)
    {
        if (requested is null) throw new ArgumentNullException(nameof(requested));
#if NET
        var path = resolver.ResolveAssemblyToPath(requested);
        return path is null ? null : AssemblyCandidate.TryCreate(path, sourceName, allowedRoot);
#else
        return null;
#endif
    }
}
