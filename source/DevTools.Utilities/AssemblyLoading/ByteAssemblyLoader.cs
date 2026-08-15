using System.Reflection;
#if NET
using System.Runtime.Loader;
#endif

namespace DevTools.Utilities.AssemblyLoading;

/// <summary>
/// Loads custom assemblies from disk via byte arrays or streams to avoid locking build output.
/// Use for hot-reload scenarios (.NET Framework AppDomain and collectible ALC on .NET 8+).
/// </summary>
public static class ByteAssemblyLoader
{
    /// <summary>
    /// Loads an assembly (and optional PDB) into the current load context from file bytes.
    /// </summary>
    public static Assembly LoadFromFile(string assemblyPath)
    {
        var assemblyBytes = File.ReadAllBytes(assemblyPath);
        var pdbPath = Path.ChangeExtension(assemblyPath, ".pdb");

        if (File.Exists(pdbPath))
            return Assembly.Load(assemblyBytes, File.ReadAllBytes(pdbPath));

        return Assembly.Load(assemblyBytes);
    }

#if NET
    /// <summary>
    /// Loads an assembly into <paramref name="context"/> using in-memory streams (no file lock).
    /// </summary>
    public static Assembly LoadFromStream(AssemblyLoadContext context, string assemblyPath)
    {
        var assemblyBytes = File.ReadAllBytes(assemblyPath);
        var pdbPath = Path.ChangeExtension(assemblyPath, ".pdb");

        if (File.Exists(pdbPath))
        {
            var pdbBytes = File.ReadAllBytes(pdbPath);
            using var asmStream = new MemoryStream(assemblyBytes);
            using var pdbStream = new MemoryStream(pdbBytes);
            return context.LoadFromStream(asmStream, pdbStream);
        }

        using var standaloneAsmStream = new MemoryStream(assemblyBytes);
        return context.LoadFromStream(standaloneAsmStream);
    }

    /// <summary>
    /// Loads an assembly into <paramref name="context"/> using open file streams.
    /// </summary>
    public static Assembly LoadFromFileStream(AssemblyLoadContext context, string assemblyPath)
    {
        using var stream = new FileStream(assemblyPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var pdbPath = Path.ChangeExtension(assemblyPath, ".pdb");

        if (!File.Exists(pdbPath))
            return context.LoadFromStream(stream);

        using var symbolStream = new FileStream(pdbPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return context.LoadFromStream(stream, symbolStream);
    }
#endif
}
