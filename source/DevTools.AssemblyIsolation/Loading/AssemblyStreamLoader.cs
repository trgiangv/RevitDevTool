using System.Reflection;
#if NET
using System.Runtime.Loader;
#endif

namespace DevTools.AssemblyIsolation.Loading;

/// <summary>
/// Loads PE/PDB without locking the source file.
/// Collectible contexts use <see cref="Load(AssemblyLoadContext, string)"/>.
/// Default AppDomain uses <see cref="Load(string)"/> (<c>Assembly.Load(byte[])</c>).
/// <see cref="LoadFile"/> is only for same-identity copies that must stay distinct.
/// </summary>
public static class AssemblyStreamLoader
{
    public static Assembly Load(string path)
    {
        var assemblyBytes = File.ReadAllBytes(path);
        var symbolPath = Path.ChangeExtension(path, ".pdb");
        return File.Exists(symbolPath)
            ? Assembly.Load(assemblyBytes, File.ReadAllBytes(symbolPath))
            : Assembly.Load(assemblyBytes);
    }

    public static Assembly LoadFile(string path) =>
        Assembly.LoadFile(Path.GetFullPath(path));

#if NET
    public static Assembly Load(AssemblyLoadContext context, string path)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));
        var assemblyBytes = File.ReadAllBytes(path);
        var symbolPath = Path.ChangeExtension(path, ".pdb");
        using var assemblyStream = new MemoryStream(assemblyBytes, writable: false);
        if (!File.Exists(symbolPath))
            return context.LoadFromStream(assemblyStream);

        using var symbolStream = new MemoryStream(File.ReadAllBytes(symbolPath), writable: false);
        return context.LoadFromStream(assemblyStream, symbolStream);
    }
#endif
}
