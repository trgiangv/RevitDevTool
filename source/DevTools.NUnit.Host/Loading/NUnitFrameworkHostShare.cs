#if NET
using System.Reflection;
using System.Runtime.Loader;
using DevTools.Utilities.AssemblyLoading;

namespace DevTools.NUnit.Host.Loading;

/// <summary>
/// Loads our generation's <c>nunit.framework</c> into a non-collectible host ALC
/// (Plugin or Default) so collectible generation ALCs do not own NUnit statics /
/// <c>AsyncLocal</c> roots. Version identity is pinned to the shadow copy — Dynamo
/// or other conflicting <c>nunit.framework</c> instances in Default are ignored.
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
        ArgumentException.ThrowIfNullOrWhiteSpace(shadowFrameworkPath);

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
                if (!NUnitGenerationManagedAssemblyIndex.IsCompatibleIdentity(identity, _shared.GetName()))
                {
                    throw new InvalidOperationException(
                        $"Host-shared nunit.framework '{_shared.FullName}' is incompatible with generation shadow '{identity.FullName}'.");
                }

                return _shared;
            }

            var hostContext = AssemblyLoadContext.GetLoadContext(typeof(NUnitFrameworkHostShare).Assembly)
                ?? AssemblyLoadContext.Default;

            if (TryFindCompatible(hostContext, identity, out var fromHost))
            {
                _shared = fromHost;
                return _shared;
            }

            // Reuse Default only when the already-loaded identity matches our shadow
            // (same major.minor/token). Never bind to Dynamo / stub 3.x copies.
            if (!ReferenceEquals(hostContext, AssemblyLoadContext.Default)
                && TryFindCompatible(AssemblyLoadContext.Default, identity, out var fromDefault))
            {
                _shared = fromDefault;
                return _shared;
            }

            _shared = ByteAssemblyLoader.LoadFromStream(hostContext, fullPath);
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
        AssemblyLoadContext context,
        AssemblyName required,
        out Assembly assembly)
    {
        foreach (var loaded in context.Assemblies)
        {
            if (!IsFrameworkSimpleName(loaded.GetName().Name))
                continue;

            if (!NUnitGenerationManagedAssemblyIndex.IsCompatibleIdentity(required, loaded.GetName()))
                continue;

            assembly = loaded;
            return true;
        }

        assembly = null!;
        return false;
    }
}
#endif
