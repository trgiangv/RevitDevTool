#if NETFRAMEWORK
using System.Reflection;
using System.Runtime.InteropServices;
using DevTools.AssemblyIsolation.Sources;

namespace DevTools.AssemblyIsolation.Loading;

/// <summary>
/// Eager-loads unmanaged sibling DLLs under an allowed root. net48 has no ALC
/// unmanaged callback, so mixed-mode command dependencies must be mapped before
/// the managed entry loads.
/// </summary>
public static class NativeLibraryPreloader
{
    public static void LoadUnmanagedFromDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("A directory is required.", nameof(directory));

        var root = Path.GetFullPath(directory);
        if (!Directory.Exists(root))
            return;

        foreach (var path in Directory.GetFiles(root, AssemblyCandidate.SearchPattern))
        {
            if (!AssemblyCandidate.IsExistingPathUnderRoot(path, root))
                continue;
            if (IsManaged(path))
                continue;

            LoadLibrary(path);
        }
    }

    private static bool IsManaged(string path)
    {
        try
        {
            AssemblyName.GetAssemblyName(path);
            return true;
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

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr LoadLibrary(string lpFileName);
}
#endif
