using System.Reflection;
using DevTools.AssemblyIsolation.Identity;

namespace DevTools.AssemblyIsolation.Sources;

public sealed class DirectoryAssemblySource : IManagedAssemblySource
{
    public DirectoryAssemblySource(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("A directory is required.", nameof(directory));

        Root = Path.GetFullPath(directory);
    }

    public string Root { get; }

    public AssemblyCandidate? Resolve(AssemblyName requested)
    {
        ArgumentNullException.ThrowIfNull(requested);

        var simpleName = requested.Name;
        if (string.IsNullOrWhiteSpace(simpleName) || !IsSimpleFileName(simpleName))
            return null;

        var candidate = AssemblyCandidate.TryCreate(AssemblyCandidate.Combine(Root, simpleName), Root);
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

    private static bool IsSimpleFileName(string name) =>
        string.Equals(name, Path.GetFileName(name), StringComparison.Ordinal)
        && name.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) < 0;
}
