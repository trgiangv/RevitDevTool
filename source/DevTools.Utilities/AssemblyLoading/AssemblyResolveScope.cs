using System.Reflection;
#if NET
using System.Runtime.Loader;
#endif

namespace DevTools.Utilities.AssemblyLoading;

/// <summary>
/// Scoped <see cref="AppDomain.AssemblyResolve"/> and <see cref="AssemblyLoadContext.Resolving"/> hooks.
/// </summary>
public sealed class AssemblyResolveScope : IDisposable
{
    private readonly Func<AssemblyName, Assembly?> _resolve;
    private readonly ResolveEventHandler _appDomainHandler;
#if NET
    private readonly List<(AssemblyLoadContext Context, Func<AssemblyLoadContext, AssemblyName, Assembly?> Handler)> _alcHandlers = [];
#endif

    public AssemblyResolveScope(Func<AssemblyName, Assembly?> resolve)
    {
        _resolve = resolve;
        _appDomainHandler = (_, args) =>
        {
            if (args.Name is null)
                return null;

            return _resolve(new AssemblyName(args.Name));
        };

        AppDomain.CurrentDomain.AssemblyResolve += _appDomainHandler;
#if NET
        RegisterLoadContext(AssemblyLoadContext.Default);
#endif
    }

#if NET
    public void RegisterLoadContextsForAssemblyPath(string assemblyPath)
    {
        var fullPath = Path.GetFullPath(assemblyPath);
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            string location;
            try
            {
                location = assembly.Location;
            }
            catch
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(location)
                || !string.Equals(Path.GetFullPath(location), fullPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var context = AssemblyLoadContext.GetLoadContext(assembly);
            if (context is not null)
                RegisterLoadContext(context);
        }
    }

    public void RegisterLoadContext(AssemblyLoadContext context)
    {
        if (_alcHandlers.Any(entry => ReferenceEquals(entry.Context, context)))
            return;

        Func<AssemblyLoadContext, AssemblyName, Assembly?> handler = (_, name) => _resolve(name);
        context.Resolving += handler;
        _alcHandlers.Add((context, handler));
    }
#endif

    public void Dispose()
    {
        AppDomain.CurrentDomain.AssemblyResolve -= _appDomainHandler;
#if NET
        foreach (var (context, handler) in _alcHandlers)
            context.Resolving -= handler;

        _alcHandlers.Clear();
#endif
    }
}
