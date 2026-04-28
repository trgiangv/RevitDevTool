using System.Diagnostics;
using System.IO;
using System.Reflection;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Providers.Dotnet;

namespace RevitDevTool.HostAdapters;

public sealed class RevitCommandDiscovery : ICommandDiscovery
{
    private static readonly string CommandFullName = typeof(IExternalCommand).FullName!;

    public List<CommandItem> ParseCommands(string assemblyPath)
    {
        if (!File.Exists(assemblyPath))
        {
            Trace.TraceError($"Assembly file not found: {assemblyPath}");
            return [];
        }

        var commands = new List<CommandItem>();
        try
        {
            var paths = CollectAssemblyPaths(assemblyPath);
            var resolver = new PathAssemblyResolver(paths);
            using var mlc = new MetadataLoadContext(resolver);

            var assembly = mlc.LoadFromAssemblyPath(assemblyPath);
            var revitApiAssembly = typeof(IExternalCommand).Assembly;
            var revitApiInContext = mlc.LoadFromAssemblyPath(revitApiAssembly.Location);
            var iExternalCommandType = revitApiInContext.GetType(CommandFullName);

            if (iExternalCommandType == null)
            {
                Trace.TraceError($"Could not find {CommandFullName} in metadata context");
                return commands;
            }

            foreach (var type in GetMetadataTypes(assembly))
            {
                if (type.IsAbstract || type.IsInterface) continue;
                if (!ImplementsInterface(type, iExternalCommandType)) continue;
                commands.Add(new CommandItem(assemblyPath, type.FullName!));
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError($"Failed to parse commands from '{assemblyPath}': {ex.Message}");
        }

        return commands;
    }

    private static List<string> CollectAssemblyPaths(string assemblyPath)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddDllsFromDirectory(paths, Path.GetDirectoryName(assemblyPath));
        AddDllsFromDirectory(paths, Path.GetDirectoryName(typeof(IExternalCommand).Assembly.Location));
        AddDllsFromDirectory(paths, Path.GetDirectoryName(typeof(object).Assembly.Location));
        AddDllsFromDirectory(paths, Path.GetDirectoryName(typeof(System.Windows.Window).Assembly.Location));
        AddDllsFromDirectory(paths, Path.GetDirectoryName(typeof(System.Windows.Media.Visual).Assembly.Location));
        AddDllsFromDirectory(paths, Path.GetDirectoryName(typeof(System.Windows.DependencyObject).Assembly.Location));
        AddDllsFromDirectory(paths, Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));

        return DeduplicateByAssemblyName(paths);
    }

    /// <summary>
    /// PathAssemblyResolver throws if two paths resolve to the same assembly identity.
    /// Keep only the first path encountered for each assembly full name.
    /// </summary>
    private static List<string> DeduplicateByAssemblyName(HashSet<string> paths)
    {
        var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            try
            {
                var name = AssemblyName.GetAssemblyName(path).FullName;
                seen.TryAdd(name, path);
            }
            catch
            {
                seen.TryAdd(path, path);
            }
        }
        return seen.Values.ToList();
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
                    types.Add(type!);
                }
                catch
                {
                    // skip types that fail to load
                }
            }
            return types;
        }
    }

    private static bool ImplementsInterface(Type type, Type interfaceType)
    {
        return type.GetInterfaces().Any(i =>
            i.FullName != null && i.FullName == interfaceType.FullName);
    }
}
