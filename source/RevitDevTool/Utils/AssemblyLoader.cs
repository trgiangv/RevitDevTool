using System.IO;
using System.Reflection;
#if !NETFRAMEWORK
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

    private static readonly string[] IsolatedAssemblies =
    [
        "MahApps.Metro",
        "ControlzEx",
        "Microsoft.Xaml.Behaviors"
    ];

#if !NETFRAMEWORK
    private static AssemblyLoadContext? _context;
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
        _context = new AssemblyLoadContext("RevitDevTool", isCollectible: false);
        AssemblyLoadContext.Default.Resolving += OnResolving;
        PreloadAssemblies();
#endif
    }

#if NETFRAMEWORK
    private static Assembly? OnAssemblyResolve(object? sender, ResolveEventArgs args)
    {
        if (string.IsNullOrEmpty(_pluginDirectory)) return null;

        var name = new AssemblyName(args.Name).Name;
        if (name == null || !IsIsolated(name)) return null;

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
    private static void PreloadAssemblies()
    {
        if (string.IsNullOrEmpty(_pluginDirectory) || _context == null) return;

        foreach (var name in IsolatedAssemblies)
        {
            var path = Path.Combine(_pluginDirectory, $"{name}.dll");
            if (!File.Exists(path)) continue;
            try
            {
                _context.LoadFromAssemblyPath(path);
            }
            catch
            {
                // Ignore - may already be loaded
            }
        }
    }

    private static Assembly? OnResolving(AssemblyLoadContext context, AssemblyName assemblyName)
    {
        if (string.IsNullOrEmpty(_pluginDirectory) || _context == null) return null;
        if (assemblyName.Name == null || !IsIsolated(assemblyName.Name)) return null;

        var path = Path.Combine(_pluginDirectory, $"{assemblyName.Name}.dll");
        if (!File.Exists(path)) return null;

        try
        {
            return _context.LoadFromAssemblyPath(path);
        }
        catch
        {
            return null;
        }
    }
#endif

    private static bool IsIsolated(string name)
    {
        return IsolatedAssemblies.Any(isolated => string.Equals(isolated, name, StringComparison.OrdinalIgnoreCase));
    }
        
    // Source - https://stackoverflow.com/a/367798
    // Posted by lubos hasko, modified by community. See post 'Timeline' for change history
    // Retrieved 2026-01-30, License - CC BY-SA 4.0
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
