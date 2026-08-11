#if NET
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using DevTools.Utilities.AssemblyLoading;
using Microsoft.Extensions.Logging;
using ZLogger;
namespace DevTools.Execution.Providers.Dotnet;

/// <summary>
/// Collectible ALC for loading command assemblies in isolation on .NET 8+.
/// Host/shared assemblies delegate to the default context; custom deps load via bytes.
/// </summary>
public class CommandLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public static void Configure(string hostApiDirectory, string hostAddinDirectory) =>
        HostSharedAssemblies.Configure(hostApiDirectory, hostAddinDirectory);

    public CommandLoadContext(string pluginPath, ILogger? logger = null) : base(name: $"DevTools_{Guid.NewGuid():N}", isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
        var pluginDirectory = Path.GetDirectoryName(pluginPath) ?? string.Empty;
        PreloadAssemblies(this, pluginDirectory);
        _logger = logger;
    }

    private readonly ILogger? _logger;

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (assemblyName.Name is { } name && HostSharedAssemblies.IsShared(name))
            return null;

        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        return assemblyPath != null ? LoadFromAssemblyPathStream(assemblyPath) : null;
    }

    private static void PreloadAssemblies(CommandLoadContext context, string pluginDirectory)
    {
        if (string.IsNullOrEmpty(pluginDirectory) || !Directory.Exists(pluginDirectory))
            return;

        foreach (var dllPath in Directory.GetFiles(pluginDirectory, "*.dll"))
        {
            var simpleName = Path.GetFileNameWithoutExtension(dllPath);
            if (HostSharedAssemblies.IsShared(simpleName))
                continue;

            context.LoadFromAssemblyPathStream(dllPath);
        }
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return libraryPath != null ? LoadUnmanagedDllFromPath(libraryPath) : IntPtr.Zero;
    }

    private Assembly? LoadFromAssemblyPathStream(string assemblyPath)
    {
        try
        {
            return ByteAssemblyLoader.LoadFromStream(this, assemblyPath);
        }
        catch (Exception ex)
        {
            var fileName = Path.GetFileName(assemblyPath);
            _logger?.ZLogError($"{nameof(CommandLoadContext)} Failed to load '{fileName}': {ex.Message}");
            return null;
        }
    }
}
#endif
