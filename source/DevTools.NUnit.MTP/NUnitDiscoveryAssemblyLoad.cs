using System.Reflection;
#if NETCOREAPP
using System.Runtime.Loader;
#endif

namespace DevTools.NUnit.MTP;

/// <summary>
/// Loads the test assembly for ExploreTests. When compile refs are listed
/// beside the exe, do not reuse the testhost entry assembly (already loaded
/// without those APIs). Resolve NuGet paths, then load an isolated copy.
/// </summary>
internal sealed class NUnitDiscoveryAssemblyLoad : IDisposable
{
    private readonly ResolveEventHandler? _resolve;
#if NETCOREAPP
    private readonly AssemblyLoadContext? _loadContext;
#endif

    private NUnitDiscoveryAssemblyLoad(
        Assembly assembly,
        ResolveEventHandler? resolve
#if NETCOREAPP
        , AssemblyLoadContext? loadContext
#endif
        )
    {
        Assembly = assembly;
        _resolve = resolve;
#if NETCOREAPP
        _loadContext = loadContext;
#endif
    }

    public Assembly Assembly { get; }

    public static NUnitDiscoveryAssemblyLoad Open(string assemblyPath)
    {
        assemblyPath = Path.GetFullPath(assemblyPath);
        var refs = NUnitDiscoveryRefs.Read(assemblyPath);
        return refs.Count == 0
            ? new NUnitDiscoveryAssemblyLoad(
                LoadShared(assemblyPath),
                resolve: null
#if NETCOREAPP
                , loadContext: null
#endif
                )
            : LoadIsolated(assemblyPath, refs);
    }

    public void Dispose()
    {
        if (_resolve is not null)
            AppDomain.CurrentDomain.AssemblyResolve -= _resolve;
#if NETCOREAPP
        _ = _loadContext;
#endif
    }

    private static NUnitDiscoveryAssemblyLoad LoadIsolated(
        string assemblyPath,
        IReadOnlyDictionary<string, string> refs)
    {
#if NETCOREAPP
        var loadContext = new DiscoveryLoadContext(refs);
        var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
        return new NUnitDiscoveryAssemblyLoad(assembly, resolve: null, loadContext);
#else
        ResolveEventHandler resolve = (_, args) => ResolveFromRefs(refs, args.Name);
        AppDomain.CurrentDomain.AssemblyResolve += resolve;
        var assembly = Assembly.LoadFile(assemblyPath);
        return new NUnitDiscoveryAssemblyLoad(assembly, resolve);
#endif
    }

    private static Assembly LoadShared(string assemblyPath)
    {
        var entry = Assembly.GetEntryAssembly();
        if (entry is not null
            && !string.IsNullOrWhiteSpace(entry.Location)
            && string.Equals(Path.GetFullPath(entry.Location), assemblyPath, StringComparison.OrdinalIgnoreCase))
        {
            return entry;
        }

        foreach (var loaded in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                if (!loaded.IsDynamic
                    && !string.IsNullOrWhiteSpace(loaded.Location)
                    && string.Equals(Path.GetFullPath(loaded.Location), assemblyPath, StringComparison.OrdinalIgnoreCase))
                {
                    return loaded;
                }
            }
            catch (NotSupportedException)
            {
            }
        }

        return Assembly.LoadFrom(assemblyPath);
    }

    private static Assembly? ResolveFromRefs(IReadOnlyDictionary<string, string> refs, string rawName)
    {
        AssemblyName requested;
        try
        {
            requested = new AssemblyName(rawName);
        }
        catch (FileLoadException)
        {
            return null;
        }

        if (requested.Name is null)
            return null;

        return refs.TryGetValue(requested.Name, out var path)
            ? Assembly.LoadFrom(path)
            : null;
    }

#if NETCOREAPP
    private sealed class DiscoveryLoadContext : AssemblyLoadContext
    {
        private readonly IReadOnlyDictionary<string, string> _refs;

        public DiscoveryLoadContext(IReadOnlyDictionary<string, string> refs)
            : base(isCollectible: false)
        {
            _refs = refs;
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name is not null
                && _refs.TryGetValue(assemblyName.Name, out var refPath))
            {
                return LoadFromAssemblyPath(refPath);
            }

            // Share NUnit.Framework / testhost assemblies with the default
            // context. Loading output copies here duplicates those types and
            // ExploreTests sees no fixtures.
            return null;
        }
    }
#endif
}
