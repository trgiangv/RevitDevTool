using System.IO;
using System.Reflection;
#if NETCOREAPP
using System.Collections.Concurrent;
using System.Runtime.Loader;
#endif

namespace RevitDevTool.Utils;

/// <summary>
/// Assembly loader for isolating plugin dependencies.
/// .NET 8+: Uses AssemblyLoadContext
/// .NET Framework: Uses AppDomain.AssemblyResolve
/// </summary>
public static class AssemblyLoader
{
    private static bool _initialized;
    private static string? _pluginDirectory;

#if NETCOREAPP
    private static PluginLoadContext _context = null!;
    private static readonly ConcurrentDictionary<string, Assembly?> Cache = new(StringComparer.OrdinalIgnoreCase);
#endif

    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        var assembly = typeof(AssemblyLoader).Assembly;
        _pluginDirectory = Path.GetDirectoryName(assembly.Location);

#if NETFRAMEWORK
        AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
#else
        _context = new PluginLoadContext(_pluginDirectory!);
        AssemblyLoadContext.Default.Resolving += OnResolving;
#endif
    }

#if NETFRAMEWORK
    private static Assembly? OnAssemblyResolve(object? sender, ResolveEventArgs args)
    {
        if (string.IsNullOrEmpty(_pluginDirectory)) return null;

        var name = new AssemblyName(args.Name).Name;
        if (name == null) return null;

        var path = Path.Combine(_pluginDirectory, $"{name}.dll");
        if (!File.Exists(path)) return null;

        try
        {
            return Assembly.LoadFrom(path);
        }
        catch
        {
            return null;
        }
    }
#else
    private sealed class PluginLoadContext(string directory) : AssemblyLoadContext("RevitDevTool", isCollectible: false)
    {
        protected override Assembly? Load(AssemblyName assemblyName)
        {
            var name = assemblyName.Name;
            if (name == null) return null;
            var path = Path.Combine(directory, $"{name}.dll");
            return !File.Exists(path) ? null : LoadFromPathCached(path);
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            var path = Path.Combine(directory, $"{unmanagedDllName}.dll");
            return File.Exists(path) ? LoadUnmanagedDllFromPath(path) : IntPtr.Zero;
        }
    }

    private static Assembly? OnResolving(AssemblyLoadContext context, AssemblyName assemblyName)
    {
        if (assemblyName.Name == null) return null;
        var path = Path.Combine(_pluginDirectory!, $"{assemblyName.Name}.dll");
        return !File.Exists(path) ? null : LoadFromPathCached(path);
    }

    private static Assembly? LoadFromPathCached(string path)
    {
        return Cache.GetOrAdd(path, p =>
        {
            try
            {
                return _context.LoadFromAssemblyPath(p);
            }
            catch
            {
                return null;
            }
        });
    }

#endif

    // Source - https://stackoverflow.com/a/367798
    public static bool IsManagedAssembly(string fileName)
    {
        try
        {
            using var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new BinaryReader(fs);

            // 1. DOS Header (MZ)
            if (fs.Length < 64) return false;
            if (reader.ReadUInt16() != 0x5A4D) return false; // "MZ"

            // 2. PE Header
            fs.Position = 0x3C;
            var peHeaderOffset = reader.ReadUInt32();
            if (peHeaderOffset > fs.Length) return false;

            fs.Position = peHeaderOffset;
            if (reader.ReadUInt32() != 0x00004550) return false; // "PE\0\0"
            
            fs.Position = peHeaderOffset + 20;
            var optionalHeaderSize = reader.ReadUInt16();
            
            fs.Position = peHeaderOffset + 24 + 96;

            if (optionalHeaderSize < 112 + 8) return false;

            var clrHeaderRva = reader.ReadUInt32();
            return clrHeaderRva != 0;
        }
        catch
        {
            return false;
        }
    }
}