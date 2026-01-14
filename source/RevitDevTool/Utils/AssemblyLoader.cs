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
}
