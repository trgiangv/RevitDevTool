using DevTools.NUnit.Core.Runtime;
using DevTools.Utilities.AssemblyLoading;

namespace DevTools.NUnit.Host.Loading;

/// <summary>
/// Host/platform assemblies that should reuse the host-loaded copy.
/// Host NuGet/API prefixes come from <see cref="HostSharedAssemblies.HostPackagePrefixes"/>.
/// <c>System.*</c> facades are shared; <c>Microsoft.*</c> is not.
/// On net48, NuGet BCL polyfills stay generation-private.
/// </summary>
public static class NUnitSharedAssemblyPolicy
{
    private static readonly string CoreContractAssemblyName =
        typeof(INUnitRuntimeSession).Assembly.GetName().Name!;

    /// <summary>
    /// BCL names that are not <c>System.*</c> (no dot after System, or netstandard).
    /// <c>System.*</c> facades are matched by prefix in <see cref="IsShared"/>.
    /// </summary>
    private static readonly HashSet<string> PlatformAssemblyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "mscorlib",
        "netstandard",
        "System",
        "System.Core",
        "System.Private.CoreLib",
        "Microsoft.Win32.Registry",
    };

#if NETFRAMEWORK
    /// <summary>
    /// NuGet polyfills that must stay generation-private on net48 so Runtime binds
    /// coherently. On modern TFMs the host/Default copy is preferred instead.
    /// </summary>
    private static readonly HashSet<string> NetfxPrivateFacades = new(StringComparer.OrdinalIgnoreCase)
    {
        "System.Reflection.Metadata",
        "System.Collections.Immutable",
        "System.Memory",
        "System.Buffers",
        "System.Runtime.CompilerServices.Unsafe",
        "System.Numerics.Vectors",
        "System.Text.Encoding.CodePages",
    };
#endif

    public static bool IsShared(string assemblySimpleName)
    {
        if (string.IsNullOrWhiteSpace(assemblySimpleName))
            return false;

        if (string.Equals(
                assemblySimpleName,
                CoreContractAssemblyName,
                StringComparison.OrdinalIgnoreCase))
            return true;

        if (PlatformAssemblyNames.Contains(assemblySimpleName)
            || HostSharedAssemblies.IsExplicitHostAssembly(assemblySimpleName)
            || HostSharedAssemblies.MatchesHostPackagePrefix(assemblySimpleName))
            return true;

        if (!assemblySimpleName.StartsWith("System.", StringComparison.OrdinalIgnoreCase))
            return false;

#if NETFRAMEWORK
        if (NetfxPrivateFacades.Contains(assemblySimpleName))
            return false;
#endif

        return true;
    }

    public static bool ShouldExcludeFromGenerationCopy(string filePath)
    {
        if (!string.Equals(Path.GetExtension(filePath), ".dll", StringComparison.OrdinalIgnoreCase))
            return false;

        if (string.Equals(
                Path.GetFileName(filePath),
                NUnitGenerationBuilder.RuntimeAssemblyFileName,
                StringComparison.OrdinalIgnoreCase))
            return true;

        if (!TryGetManagedAssemblyIdentity(filePath, out var identity) || identity is null)
            return false;

        return IsShared(identity);
    }

    internal static bool TryGetManagedAssemblyIdentity(string filePath, out string? identity)
    {
        identity = null;
        if (!IsManagedAssemblyFile(filePath))
            return false;

        try
        {
            identity = System.Reflection.AssemblyName.GetAssemblyName(filePath).Name;
            return !string.IsNullOrWhiteSpace(identity);
        }
        catch (BadImageFormatException)
        {
            return false;
        }
        catch (FileLoadException)
        {
            return false;
        }
    }

    internal static bool IsManagedAssemblyFile(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return extension.Equals(".dll", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".exe", StringComparison.OrdinalIgnoreCase);
    }
}
