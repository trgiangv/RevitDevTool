using System.Reflection;
#if NET
using System.Runtime.Loader;
#endif

namespace DevTools.AssemblyIsolation.Loading;

public static class AssemblyStreamLoader
{
    public static Assembly Load(string path)
    {
#if NETFRAMEWORK
        // Same-identity generations must stay distinct in the default AppDomain.
        // Load(byte[]) unifies by identity and would mix hot-reload copies.
        // The path is a shadow/temp copy, not the developer's project output.
        return Assembly.LoadFile(Path.GetFullPath(path));
#else
        var assemblyBytes = File.ReadAllBytes(path);
        var symbolPath = Path.ChangeExtension(path, ".pdb");
        return File.Exists(symbolPath)
            ? Assembly.Load(assemblyBytes, File.ReadAllBytes(symbolPath))
            : Assembly.Load(assemblyBytes);
#endif
    }

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
