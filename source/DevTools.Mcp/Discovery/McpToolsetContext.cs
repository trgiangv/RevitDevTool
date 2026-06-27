using System.Diagnostics;
using System.Reflection;
#if NET
using System.Runtime.Loader;
#endif

namespace DevTools.Mcp.Discovery;

/// <summary>
/// Wraps a per-toolset assembly load context for isolation.
/// .NET 8+: collectible AssemblyLoadContext with AssemblyDependencyResolver.
/// .NET Framework: byte-array load with scoped AssemblyResolve handler.
/// </summary>
public sealed class McpToolsetContext(string toolsetDllPath) : IDisposable
{
    private readonly string _toolsetPath = Path.GetFullPath(toolsetDllPath);
    private Assembly? _loadedAssembly;
    private bool _disposed;

#if NET
    private ToolsetLoadContext? _loadContext;
#else
    private ResolveEventHandler? _resolveHandler;
#endif

    public Assembly LoadAssembly()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(McpToolsetContext));

        if (_loadedAssembly is not null)
            return _loadedAssembly;

#if NET
        var fileName = Path.GetFileNameWithoutExtension(_toolsetPath);
        _loadContext = new ToolsetLoadContext(_toolsetPath, $"McpToolset_{fileName}");
        _loadedAssembly = _loadContext.LoadEntryAssembly();
#else
        var toolsetDir = Path.GetDirectoryName(_toolsetPath) ?? string.Empty;
        _resolveHandler = (_, args) => ResolveFromDirectory(toolsetDir, args);
        AppDomain.CurrentDomain.AssemblyResolve += _resolveHandler;
        _loadedAssembly = Assembly.Load(File.ReadAllBytes(_toolsetPath));
#endif
        return _loadedAssembly;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

#if NET
        _loadContext?.Unload();
        _loadContext = null;
#else
        if (_resolveHandler is not null)
        {
            AppDomain.CurrentDomain.AssemblyResolve -= _resolveHandler;
            _resolveHandler = null;
        }
#endif
        _loadedAssembly = null;
    }

#if NETFRAMEWORK
    private static Assembly? ResolveFromDirectory(string directory, ResolveEventArgs args)
    {
        try
        {
            var name = new AssemblyName(args.Name).Name;
            if (name is null) return null;

            var path = Path.Combine(directory, $"{name}.dll");
            return !File.Exists(path) ? null : Assembly.Load(File.ReadAllBytes(path));

        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"[McpToolsetContext] Failed to resolve '{args.Name}': {ex.Message}");
            return null;
        }
    }
#endif

#if NET
    private sealed class ToolsetLoadContext : AssemblyLoadContext
    {
        private readonly string _toolsetPath;
        private readonly string _toolsetDirectory;
        private readonly AssemblyDependencyResolver _resolver;
        private readonly bool _hasDepsJson;

        private static readonly string[] SharedPrefixes =
        [
            "System.", "Microsoft.", "MahApps.", "ControlzEx.",
            "CommunityToolkit.", "Autodesk."
        ];

        private static readonly HashSet<string> SharedAssemblyNames = BuildSharedAssemblyNames();

        private static HashSet<string> BuildSharedAssemblyNames()
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AddDllNames(names, Path.GetDirectoryName(typeof(object).Assembly.Location));

            var hostDir = Path.GetDirectoryName(typeof(McpToolsetContext).Assembly.Location);
            AddDllNames(names, hostDir);

            names.Add("ModelContextProtocol");

            return names;
        }

        private static void AddDllNames(HashSet<string> names, string? directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return;
            try
            {
                foreach (var dll in Directory.GetFiles(directory, "*.dll"))
                    names.Add(Path.GetFileNameWithoutExtension(dll));
            }
            catch
            {
                // Ignore unreadable directories.
            }
        }

        private static bool IsSharedAssembly(string assemblyName)
        {
            if (SharedAssemblyNames.Contains(assemblyName))
                return true;

            foreach (var prefix in SharedPrefixes)
            {
                if (assemblyName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public ToolsetLoadContext(string toolsetPath, string name)
            : base(name, isCollectible: true)
        {
            _toolsetPath = toolsetPath;
            _toolsetDirectory = Path.GetDirectoryName(toolsetPath) ?? string.Empty;
            _resolver = new AssemblyDependencyResolver(toolsetPath);

            var depsPath = Path.ChangeExtension(toolsetPath, ".deps.json");
            _hasDepsJson = File.Exists(depsPath);
        }

        public Assembly LoadEntryAssembly()
        {
            if (!_hasDepsJson)
                PreloadSiblingAssemblies();

            return LoadFromAssemblyStream(_toolsetPath);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name is not null && IsSharedAssembly(assemblyName.Name))
                return null;

            var resolvedPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (resolvedPath is not null)
                return LoadFromAssemblyStream(resolvedPath);

            if (!_hasDepsJson && assemblyName.Name is not null)
            {
                var siblingPath = Path.Combine(_toolsetDirectory, $"{assemblyName.Name}.dll");
                if (File.Exists(siblingPath))
                    return LoadFromAssemblyStream(siblingPath);
            }

            return null;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            return libraryPath is not null ? LoadUnmanagedDllFromPath(libraryPath) : IntPtr.Zero;
        }

        private void PreloadSiblingAssemblies()
        {
            if (!Directory.Exists(_toolsetDirectory)) return;

            foreach (var dllPath in Directory.GetFiles(_toolsetDirectory, "*.dll"))
            {
                if (string.Equals(Path.GetFullPath(dllPath), _toolsetPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                var simpleName = Path.GetFileNameWithoutExtension(dllPath);
                if (IsSharedAssembly(simpleName)) continue;

                LoadFromAssemblyStream(dllPath);
            }
        }

        private Assembly LoadFromAssemblyStream(string assemblyPath)
        {
            try
            {
                var assemblyBytes = File.ReadAllBytes(assemblyPath);
                var pdbPath = Path.ChangeExtension(assemblyPath, ".pdb");

                if (File.Exists(pdbPath))
                {
                    var pdbBytes = File.ReadAllBytes(pdbPath);
                    using var asmStream = new MemoryStream(assemblyBytes);
                    using var pdbStream = new MemoryStream(pdbBytes);
                    return LoadFromStream(asmStream, pdbStream);
                }

                using var stream = new MemoryStream(assemblyBytes);
                return LoadFromStream(stream);
            }
            catch (Exception ex)
            {
                var fileName = Path.GetFileName(assemblyPath);
                Trace.TraceError($"[McpToolsetContext] Failed to load '{fileName}': {ex.Message}");
                throw;
            }
        }
    }
#endif
}
