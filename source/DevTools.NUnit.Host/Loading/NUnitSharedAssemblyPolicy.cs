using DevTools.NUnit.Core.Runtime;
using DevTools.Utilities.AssemblyLoading;

namespace DevTools.NUnit.Host.Loading;

/// <summary>
/// Host/platform assemblies that should reuse the host-loaded copy.
/// Host NuGet/API prefixes come from <see cref="HostSharedAssemblies.MatchesHostPackagePrefix"/>.
/// Versioned dependencies, including <c>System.*</c> and <c>Microsoft.*</c>
/// NuGet assemblies, stay generation-private unless explicitly declared as host-owned.
/// </summary>
public static class NUnitSharedAssemblyPolicy
{
    private static readonly string CoreContractAssemblyName =
        typeof(INUnitRuntimeSession).Assembly.GetName().Name!;

    /// <summary>
    /// Runtime assemblies with fixed platform ownership, rather than a namespace prefix.
    /// </summary>
    private static readonly HashSet<string> PlatformAssemblyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "mscorlib",
        "netstandard",
        "System",
        "System.Core",
        "System.Private.CoreLib",
    };

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

        return false;
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
