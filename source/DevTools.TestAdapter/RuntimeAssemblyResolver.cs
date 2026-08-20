using System.Reflection;

namespace DevTools.TestAdapter;

internal static class RuntimeAssemblyResolver
{
    private static int _registered;

    internal static void EnsureRegistered()
    {
        if (Interlocked.Exchange(ref _registered, 1) != 0)
            return;

        AppDomain.CurrentDomain.AssemblyResolve += ResolvePrivateRuntimeAssembly;
    }

    private static Assembly? ResolvePrivateRuntimeAssembly(object? sender, ResolveEventArgs args)
    {
        if (!TryParseFullIdentity(args.Name, out var requested))
            return null;

        var name = requested.Name!;
        var baseDirectory = Path.GetFullPath(AppContext.BaseDirectory);
        var path = Path.GetFullPath(Path.Combine(baseDirectory, name + ".dll"));
        if (!IsUnderBaseDirectory(path, baseDirectory) || !File.Exists(path))
            return null;

        try
        {
            var candidate = AssemblyName.GetAssemblyName(path);
            return HasSameFullIdentity(requested, candidate) ? Assembly.LoadFrom(path) : null;
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
    }

    private static bool TryParseFullIdentity(string? value, out AssemblyName requested)
    {
        requested = null!;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        try
        {
            requested = new AssemblyName(value);
        }
        catch (FileLoadException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }

        return requested.Name is { Length: > 0 } name
            && requested.Version is not null
            && string.Equals(name, Path.GetFileName(name), StringComparison.Ordinal)
            && name.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) < 0
            && name is not "." and not "..";
    }

    /// <summary>
    /// <see cref="Assembly.LoadFrom(string)"/> keeps a Windows file lock.
    /// Byte-load so testhost does not pin the sibling DLL during rebuild.
    /// </summary>
    internal static Assembly LoadUnlocked(string path)
    {
        var assemblyBytes = File.ReadAllBytes(path);
        var symbolPath = Path.ChangeExtension(path, ".pdb");
        return File.Exists(symbolPath)
            ? Assembly.Load(assemblyBytes, File.ReadAllBytes(symbolPath))
            : Assembly.Load(assemblyBytes);
    }

    private static bool IsUnderBaseDirectory(string path, string baseDirectory)
    {
        var root = baseDirectory.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? baseDirectory
            : baseDirectory + Path.DirectorySeparatorChar;
        return path.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasSameFullIdentity(AssemblyName requested, AssemblyName candidate) =>
        string.Equals(requested.Name, candidate.Name, StringComparison.OrdinalIgnoreCase)
        && requested.Version == candidate.Version
        && string.Equals(NormalizeCulture(requested), NormalizeCulture(candidate), StringComparison.OrdinalIgnoreCase)
        && (requested.GetPublicKeyToken() ?? []).SequenceEqual(candidate.GetPublicKeyToken() ?? []);

    private static string NormalizeCulture(AssemblyName identity) =>
        string.Equals(identity.CultureName, "neutral", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : identity.CultureName ?? string.Empty;
}
