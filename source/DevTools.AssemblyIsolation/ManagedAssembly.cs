using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace DevTools.AssemblyIsolation;

/// <summary>
/// Reads whether a file is a managed assembly without loading it.
/// Native images and unreadable files are not managed.
/// </summary>
public static class ManagedAssembly
{
    public static bool IsManaged(string path) => TryGetName(path, out _);

    public static bool TryGetName(string path, [NotNullWhen(true)] out AssemblyName? name)
    {
        name = null;
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            var identity = AssemblyName.GetAssemblyName(path);
            if (string.IsNullOrWhiteSpace(identity.Name))
                return false;

            name = identity;
            return true;
        }
        catch (Exception ex) when (ex is BadImageFormatException
                                     or FileLoadException
                                     or IOException
                                     or UnauthorizedAccessException
                                     or ArgumentException)
        {
            return false;
        }
    }
}
