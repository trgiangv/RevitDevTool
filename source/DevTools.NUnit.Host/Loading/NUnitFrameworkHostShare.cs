using System.Reflection;
#if NET
using System.Runtime.Loader;
#endif
using DevTools.AssemblyIsolation.Identity;
using DevTools.AssemblyIsolation.Loading;

namespace DevTools.NUnit.Host.Loading;

/// <summary>
/// Loads our generation's <c>nunit.framework</c> once into the host context so
/// generation isolation does not own NUnit statics / <c>AsyncLocal</c> roots.
/// Version identity is pinned to the shadow copy — Dynamo or other conflicting
/// <c>nunit.framework</c> instances are ignored unless the full identity matches.
/// </summary>
internal static class NUnitFrameworkHostShare
{
    private const string FrameworkSimpleName = "nunit.framework";

    private static readonly object Gate = new();
    private static Assembly? _shared;

    internal static bool IsFrameworkSimpleName(string? simpleName) =>
        string.Equals(simpleName, FrameworkSimpleName, StringComparison.OrdinalIgnoreCase);

    internal static Assembly GetOrLoadFromShadow(string shadowFrameworkPath)
    {
        if (string.IsNullOrWhiteSpace(shadowFrameworkPath))
            throw new ArgumentException("A shadow framework path is required.", nameof(shadowFrameworkPath));

        var fullPath = Path.GetFullPath(shadowFrameworkPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                $"Generation nunit.framework shadow path not found: {fullPath}",
                fullPath);
        }

        var identity = AssemblyName.GetAssemblyName(fullPath);
        if (!IsFrameworkSimpleName(identity.Name))
        {
            throw new InvalidOperationException(
                $"Shadow path '{fullPath}' is not nunit.framework (identity '{identity.FullName}').");
        }

        lock (Gate)
        {
            if (_shared is not null)
            {
                if (!AssemblyIdentityMatcher.IsCompatible(identity, _shared.GetName()))
                {
                    throw new InvalidOperationException(
                        $"Host-shared nunit.framework '{_shared.FullName}' is incompatible with generation shadow '{identity.FullName}'.");
                }

                return _shared;
            }

#if NET
            var hostContext = AssemblyLoadContext.GetLoadContext(typeof(NUnitFrameworkHostShare).Assembly)
                ?? AssemblyLoadContext.Default;

            if (TryFindCompatible(hostContext.Assemblies, identity, out var fromHost))
            {
                _shared = fromHost;
                return _shared;
            }

            // Reuse Default only when the already-loaded identity matches our shadow
            // (same version/token). Never bind to Dynamo / stub 3.x copies.
            if (!ReferenceEquals(hostContext, AssemblyLoadContext.Default)
                && TryFindCompatible(AssemblyLoadContext.Default.Assemblies, identity, out var fromDefault))
            {
                _shared = fromDefault;
                return _shared;
            }

            _shared = AssemblyStreamLoader.Load(hostContext, fullPath);
#else
            if (TryFindCompatible(AppDomain.CurrentDomain.GetAssemblies(), identity, out var fromDomain))
            {
                _shared = fromDomain;
                return _shared;
            }

            _shared = AssemblyStreamLoader.Load(fullPath);
#endif
            return _shared;
        }
    }

    internal static bool TryGetLoaded(out Assembly assembly)
    {
        lock (Gate)
        {
            assembly = _shared!;
            return _shared is not null;
        }
    }

    private static bool TryFindCompatible(
        IEnumerable<Assembly> assemblies,
        AssemblyName required,
        out Assembly assembly)
    {
        foreach (var loaded in assemblies)
        {
            if (!IsFrameworkSimpleName(loaded.GetName().Name))
                continue;

            if (!AssemblyIdentityMatcher.IsCompatible(required, loaded.GetName()))
                continue;

            assembly = loaded;
            return true;
        }

        assembly = null!;
        return false;
    }
}
