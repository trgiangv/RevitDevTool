using System.IO;
using System.Reflection;
using DevTools.AssemblyIsolation.Metadata;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Providers.Dotnet;
using Microsoft.Extensions.Logging;
using ZLogger;
namespace RevitDevTool.Adapters;

public sealed class RevitCommandDiscovery(ILogger<RevitCommandDiscovery> logger) : ICommandDiscovery
{
    private static readonly string CommandFullName = typeof(IExternalCommand).FullName!;

    public List<CommandItem> ParseCommands(string assemblyPath)
    {
        if (!File.Exists(assemblyPath))
        {
            logger.ZLogError($"Assembly file not found: {assemblyPath}");
            return [];
        }

        var commands = new List<CommandItem>();
        try
        {
            using var session = MetadataAssemblySession.Create(assemblyPath, CollectAssemblyPaths(assemblyPath));
            var assembly = session.LoadEntryAssembly();

            foreach (var type in GetMetadataTypes(assembly))
            {
                if (type.IsAbstract || type.IsInterface) continue;
                if (!ImplementsInterface(type, CommandFullName)) continue;
                commands.Add(new CommandItem(assemblyPath, type.FullName!));
            }
        }
        catch (Exception ex)
        {
            logger.ZLogError($"Failed to parse commands from '{assemblyPath}': {ex.Message}");
        }

        return commands;
    }

    private static HashSet<string> CollectAssemblyPaths(string assemblyPath)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddDllsFromDirectory(paths, Path.GetDirectoryName(assemblyPath));
        AddDllsFromDirectory(paths, Path.GetDirectoryName(typeof(IExternalCommand).Assembly.Location));
        AddDllsFromDirectory(paths, Path.GetDirectoryName(typeof(object).Assembly.Location));
        AddDllsFromDirectory(paths, Path.GetDirectoryName(typeof(System.Windows.Window).Assembly.Location));
        AddDllsFromDirectory(paths, Path.GetDirectoryName(typeof(System.Windows.Media.Visual).Assembly.Location));
        AddDllsFromDirectory(paths, Path.GetDirectoryName(typeof(System.Windows.DependencyObject).Assembly.Location));
        AddDllsFromDirectory(paths, Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));

        return paths;
    }

    private static void AddDllsFromDirectory(HashSet<string> paths, string? directory)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return;
        try
        {
            foreach (var dll in Directory.GetFiles(directory, "*.dll"))
                paths.Add(dll);
        }
        catch
        {
            // ignore errors scanning directories
        }
    }

    private static List<Type> GetMetadataTypes(Assembly assembly)
    {
        try
        {
            return [..assembly.GetTypes()];
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null).ToList()!;
        }
        catch
        {
            var types = new List<Type>();
            foreach (var typeInfo in assembly.DefinedTypes)
            {
                try
                {
                    var type = typeInfo.AsType();
                    _ = type.BaseType;
                    types.Add(type);
                }
                catch
                {
                    // skip types that fail to load
                }
            }
            return types;
        }
    }

    private static bool ImplementsInterface(Type type, string interfaceFullName)
    {
        return type.GetInterfaces().Any(i =>
            i.FullName != null && i.FullName == interfaceFullName);
    }
}
