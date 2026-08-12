using DevTools.NUnit.Core.Runtime;
using DevTools.Utilities.AssemblyLoading;

namespace DevTools.NUnit.Host.Loading;

/// <summary>
/// Identifies known host/platform assemblies that should reuse an assembly
/// already loaded by the host instead of taking a generation-private copy.
/// This is a preference for known cases, not a closed-world dependency policy.
/// </summary>
public static class NUnitSharedAssemblyPolicy
{
    private const string RuntimeAssemblyFileName = "DevTools.NUnit.Runtime.dll";
    private static readonly string CoreContractAssemblyName =
        typeof(INUnitRuntimeSession).Assembly.GetName().Name!;

    private static readonly HashSet<string> PlatformAssemblyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "mscorlib",
        "netstandard",
        "System",
        "System.Core",
        "System.Private.CoreLib",
        "System.Runtime",
        "System.Console",
        "System.Collections",
        "System.Collections.Concurrent",
        "System.Collections.Specialized",
        "System.ComponentModel.Primitives",
        "System.ComponentModel.TypeConverter",
        "System.Diagnostics.Process",
        "System.Diagnostics.TextWriterTraceListener",
        "System.IO.Compression",
        "System.IO.Compression.FileSystem",
        "System.IO.Compression.ZipFile",
        "System.IO.MemoryMappedFiles",
        "System.Linq",
        "System.Linq.Expressions",
        "System.Net.Http",
        "System.Net.Primitives",
        "System.Runtime.InteropServices",
        "System.Runtime.Loader",
        "System.Security.Cryptography",
        "System.Text.Encoding.Extensions",
        "System.Text.RegularExpressions",
        "System.Threading",
        "System.Threading.Thread",
        "System.Web",
        "System.Windows.Forms",
        "System.Xml",
        "System.Xml.Linq",
        "System.Xml.ReaderWriter",
        "System.Xml.XmlSerializer",
        "Microsoft.Win32.Registry",
    };

    public static bool IsShared(string assemblySimpleName)
    {
        if (string.IsNullOrWhiteSpace(assemblySimpleName))
            return false;

        // The neutral contract must retain one type identity across the load
        // boundary. Derive its name from the loaded contract instead of
        // maintaining another assembly-name list.
        if (string.Equals(
                assemblySimpleName,
                CoreContractAssemblyName,
                StringComparison.OrdinalIgnoreCase))
            return true;

        if (PlatformAssemblyNames.Contains(assemblySimpleName)
            || HostSharedAssemblies.IsExplicitHostAssembly(assemblySimpleName))
            return true;

        return assemblySimpleName.StartsWith("Autodesk.", StringComparison.OrdinalIgnoreCase)
            || assemblySimpleName.StartsWith("MahApps.", StringComparison.OrdinalIgnoreCase)
            || assemblySimpleName.StartsWith("ControlzEx.", StringComparison.OrdinalIgnoreCase)
            || assemblySimpleName.StartsWith("CommunityToolkit.", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsCoreContract(string assemblySimpleName) =>
        string.Equals(
            assemblySimpleName,
            CoreContractAssemblyName,
            StringComparison.OrdinalIgnoreCase);

    public static bool ShouldExcludeFromGenerationCopy(string filePath)
    {
        if (!string.Equals(Path.GetExtension(filePath), ".dll", StringComparison.OrdinalIgnoreCase))
            return false;

        if (string.Equals(Path.GetFileName(filePath), RuntimeAssemblyFileName, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!TryGetManagedAssemblyIdentity(filePath, out var identity) || identity is null)
            return false;

        return IsShared(identity);
    }

    internal static bool TryGetManagedAssemblyIdentity(string filePath, out string? identity)
    {
        identity = null;
        if (!string.Equals(Path.GetExtension(filePath), ".dll", StringComparison.OrdinalIgnoreCase))
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
}
