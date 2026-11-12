using Autodesk.AutoCAD.Runtime;
using DevTools.AssemblyIsolation;
using DevTools.AssemblyIsolation.Metadata;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Providers.Dotnet;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Reflection;
using ZLogger;

namespace AcadDevTool.Adapters;

/// <summary>
/// Discovers [CommandMethod] commands from .NET assemblies for AutoCAD and its verticals (Civil 3D, Map 3D, Plant 3D...).
/// Only public instance or static methods with void return and no parameters are considered.
/// <see cref="CommandItem.FullClassName"/> is stored as <c>TypeFullName.MethodName</c> so
/// <see cref="AcadCommandRunner"/> can split on the last dot into type and method (e.g.
/// <c>MyNs.MyClass.DoWork</c> → type <c>MyNs.MyClass</c>, method <c>DoWork</c>).
/// </summary>
/// <remarks>
/// Metadata is resolved against the same host assemblies <see cref="AcadCommandRunner"/> binds to, so a vertical API
/// that lives outside the AutoCAD root (Civil 3D's <c>C3D\AeccDbMgd.dll</c>) resolves during discovery too.
/// A type or method whose metadata cannot be resolved is skipped and reported; it does not hide the other commands.
/// </remarks>
public sealed class AcadCommandDiscovery(ILogger<AcadCommandDiscovery> logger, HostAssemblies hostAssemblies) : ICommandDiscovery
{
    private const string CommandMethodFullName = "Autodesk.AutoCAD.Runtime.CommandMethodAttribute";
    private const string CommandClassFullName = "Autodesk.AutoCAD.Runtime.CommandClassAttribute";
    private const int MaxReportedSkips = 5;

    private const BindingFlags CommandMethodFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;

    private readonly Lazy<IReadOnlyList<string>> _hostResolutionPaths = new(
        () => CollectHostResolutionPaths(hostAssemblies),
        LazyThreadSafetyMode.ExecutionAndPublication);

    public List<CommandItem> ParseCommands(string assemblyPath)
    {
        if (!File.Exists(assemblyPath))
        {
            logger.ZLogError($"Assembly file not found: {assemblyPath}");
            return [];
        }

        var commands = new List<CommandItem>();
        var skipped = new List<string>();
        try
        {
            using var session = MetadataAssemblySession.Create(assemblyPath, CollectAssemblyPaths(assemblyPath));
            var assembly = session.LoadEntryAssembly();

            foreach (var type in GetCommandTypes(assembly, skipped))
            {
                try
                {
                    if (!IsCandidateType(type)) continue;
                    commands.AddRange(FindCommands(type, assemblyPath, skipped));
                }
                catch (System.Exception ex) when (IsMetadataResolutionFailure(ex))
                {
                    skipped.Add($"{SafeName(type)}: {ex.Message}");
                }
            }
        }
        catch (System.Exception ex)
        {
            logger.ZLogError($"Failed to parse commands from '{assemblyPath}': {ex.Message}");
        }

        if (skipped.Count > 0)
        {
            var sample = string.Join("; ", skipped.Take(MaxReportedSkips));
            logger.ZLogWarning(
                $"Skipped {skipped.Count} type(s) or method(s) in '{assemblyPath}' whose metadata did not resolve: {sample}");
        }

        return commands;
    }

    /// <summary>
    /// AutoCAD only searches the types named by <c>[assembly: CommandClass(typeof(T))]</c> when the assembly declares
    /// any; otherwise it searches every exported type.
    /// </summary>
    private static IEnumerable<Type> GetCommandTypes(Assembly assembly, List<string> skipped)
    {
        var declared = GetDeclaredCommandClasses(assembly);
        return declared.Count > 0 ? declared : GetMetadataTypes(assembly, skipped);
    }

    private static List<Type> GetDeclaredCommandClasses(Assembly assembly)
    {
        var types = new List<Type>();
        foreach (var attribute in assembly.GetCustomAttributesData())
        {
            if (attribute.AttributeType.FullName != CommandClassFullName) continue;
            if (attribute.ConstructorArguments is [{ Value: Type type }, ..])
                types.Add(type);
        }

        return types;
    }

    private static bool IsCandidateType(Type type)
    {
        if (type.IsInterface || type.ContainsGenericParameters) return false;
        return type is not { IsAbstract: true, IsSealed: false };
    }

    private static IEnumerable<CommandItem> FindCommands(Type type, string assemblyPath, List<string> skipped)
    {
        var commands = new List<CommandItem>();
        foreach (var method in type.GetMethods(CommandMethodFlags))
        {
            try
            {
                // Attribute first: return and parameter types of unrelated methods may live in assemblies that do not
                // resolve here (WPF, vertical APIs, plugin dependencies) and must not cost the real commands.
                var globalName = GetGlobalCommandName(method);
                if (globalName is null) continue;

                // A concrete base class reports its own commands; reporting them again under every subclass would list
                // the same AutoCAD command several times.
                if (method.DeclaringType != type && method.DeclaringType is { } declaring && IsCandidateType(declaring))
                    continue;

                if (method.ReturnType.FullName != typeof(void).FullName) continue;
                if (method.GetParameters().Length != 0) continue;

                commands.Add(new CommandItem(assemblyPath, $"{type.FullName}.{method.Name}") { Name = globalName });
            }
            catch (System.Exception ex) when (IsMetadataResolutionFailure(ex))
            {
                skipped.Add($"{SafeName(type)}.{method.Name}: {ex.Message}");
            }
        }

        return commands;
    }

    /// <summary>
    /// Reads the global command name from any <see cref="CommandMethodAttribute"/> constructor.
    /// Overloads start with either <c>(globalName, ...)</c> or <c>(groupName, globalName, ...)</c>; the second form is
    /// recognised by a second <see cref="string"/> argument.
    /// </summary>
    internal static string? GetGlobalCommandName(MethodInfo method)
    {
        foreach (var attribute in method.GetCustomAttributesData())
        {
            if (!IsAttribute(attribute, CommandMethodFullName)) continue;

            var args = attribute.ConstructorArguments;
            var name = args is [{ Value: string }, { Value: string globalName }, ..]
                ? globalName
                : args.Count > 0 ? args[0].Value as string : null;
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }

        return null;
    }

    private static bool IsAttribute(CustomAttributeData attribute, string fullName)
    {
        try
        {
            return attribute.AttributeType.FullName == fullName;
        }
        catch (System.Exception ex) when (IsMetadataResolutionFailure(ex))
        {
            // An attribute from an assembly that does not resolve here cannot be [CommandMethod].
            return false;
        }
    }

    private IEnumerable<string> CollectAssemblyPaths(string assemblyPath)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddDllsFromDirectory(paths, Path.GetDirectoryName(Path.GetFullPath(assemblyPath)));
        paths.UnionWith(_hostResolutionPaths.Value);
        return paths;
    }

    /// <summary>
    /// Runtime, WPF, and every directory that holds a captured host assembly (AutoCAD root plus vertical folders such
    /// as <c>C3D\</c>). Host directories do not change during the session, so the scan runs once.
    /// </summary>
    private static IReadOnlyList<string> CollectHostResolutionPaths(HostAssemblies hostAssemblies)
    {
        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddDirectoryOf(directories, typeof(object).Assembly);
        AddDirectoryOf(directories, typeof(System.Windows.Window).Assembly);
        AddDirectoryOf(directories, typeof(System.Windows.Media.Visual).Assembly);
        AddDirectoryOf(directories, typeof(System.Windows.DependencyObject).Assembly);
        AddDirectoryOf(directories, typeof(CommandMethodAttribute).Assembly);

        foreach (var assembly in hostAssemblies.All())
            AddDirectoryOf(directories, assembly);

        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var directory in directories)
            AddDllsFromDirectory(paths, directory);

        return [.. paths];
    }

    private static void AddDirectoryOf(HashSet<string> directories, Assembly assembly)
    {
        if (assembly.IsDynamic) return;

        var location = assembly.Location;
        if (string.IsNullOrEmpty(location)) return;

        var directory = Path.GetDirectoryName(location);
        if (!string.IsNullOrEmpty(directory))
            directories.Add(directory!);
    }

    private static void AddDllsFromDirectory(HashSet<string> paths, string? directory)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return;
        try
        {
            foreach (var dll in Directory.GetFiles(directory, "*.dll"))
                paths.Add(dll);
        }
        catch (System.Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A directory that cannot be listed only narrows resolution; discovery reports what it then cannot resolve.
        }
    }

    private static IEnumerable<Type> GetMetadataTypes(Assembly assembly, List<string> skipped)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            foreach (var loaderException in ex.LoaderExceptions.OfType<System.Exception>().Take(MaxReportedSkips))
                skipped.Add(loaderException.Message);
            return ex.Types.OfType<Type>();
        }
    }

    private static bool IsMetadataResolutionFailure(System.Exception ex) =>
        ex is FileNotFoundException or FileLoadException or BadImageFormatException or TypeLoadException
            or MissingMemberException or NotSupportedException or InvalidOperationException;

    private static string SafeName(Type type)
    {
        try { return type.FullName ?? type.Name; }
        catch (System.Exception ex) when (IsMetadataResolutionFailure(ex)) { return type.Name; }
    }
}
