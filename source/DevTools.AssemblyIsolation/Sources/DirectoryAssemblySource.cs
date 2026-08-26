using System.Reflection;
using DevTools.AssemblyIsolation.Identity;

namespace DevTools.AssemblyIsolation.Sources;

public sealed class DirectoryAssemblySource : IManagedAssemblySource
{
    readonly string root;

    public DirectoryAssemblySource(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("A directory is required.", nameof(directory));

        root = Path.GetFullPath(directory);
    }

    public string Root => root;

    public AssemblyCandidate? Resolve(AssemblyName requested)
    {
        if (requested is null) throw new ArgumentNullException(nameof(requested));

        var simpleName = requested.Name;
        if (string.IsNullOrWhiteSpace(simpleName) || !IsSimpleFileName(simpleName))
            return null;

        var candidate = AssemblyCandidate.TryCreate(AssemblyCandidate.Combine(root, simpleName), root);
        if (candidate is null || !File.Exists(candidate.Path))
            return null;

        AssemblyName identity;
        try
        {
            identity = AssemblyName.GetAssemblyName(candidate.Path);
        }
        catch (BadImageFormatException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        return AssemblyIdentityMatcher.IsCompatible(requested, identity) ? candidate : null;
    }

    static bool IsSimpleFileName(string name) =>
        string.Equals(name, Path.GetFileName(name), StringComparison.Ordinal)
        && name.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) < 0;
}
