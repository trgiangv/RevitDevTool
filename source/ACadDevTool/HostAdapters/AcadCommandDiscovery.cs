using System.Diagnostics;
using System.IO;
using System.Reflection;
using Autodesk.AutoCAD.Runtime;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Providers.Dotnet;
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace AcadDevTool.HostAdapters;

/// <summary>
/// Discovers [CommandMethod] commands from .NET assemblies for AutoCAD.
/// Only public instance or static methods with void return and no parameters are considered.
/// <see cref="CommandItem.FullClassName"/> is stored as <c>TypeFullName.MethodName</c> so
/// <see cref="AcadCommandRunner"/> can split on the last dot into type and method (e.g.
/// <c>MyNs.MyClass.DoWork</c> → type <c>MyNs.MyClass</c>, method <c>DoWork</c>).
/// </summary>
public sealed class AcadCommandDiscovery : ICommandDiscovery
{
    private static readonly string CommandMethodFullName = typeof(CommandMethodAttribute).FullName!;
    private static readonly string VoidFullName = typeof(void).FullName!;

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

            foreach (var type in GetMetadataTypes(assembly))
            {
                if (!IsCandidateType(type)) continue;
                commands.AddRange(FindCommands(type, assemblyPath));
            }
        }
        catch (System.Exception ex)
        {
            Trace.TraceError($"Failed to parse commands from '{assemblyPath}': {ex.Message}");
        }

        return commands;
    }

    private static bool IsCandidateType(Type type)
    {
        if (type.IsInterface) return false;
        return type is not { IsAbstract: true, IsSealed: false };
    }

    private static IEnumerable<CommandItem> FindCommands(Type type, string assemblyPath)
    {
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
        {
            if (method.ReturnType.FullName != VoidFullName) continue;
            if (method.GetParameters().Length != 0) continue;

            var attr = method.CustomAttributes.FirstOrDefault(a =>
                a.AttributeType.FullName == CommandMethodFullName);
            if (attr == null) continue;

            var globalName = attr.ConstructorArguments.FirstOrDefault().Value?.ToString();
            if (string.IsNullOrEmpty(globalName)) continue;

            yield return new CommandItem(assemblyPath, $"{type.FullName}.{method.Name}") { Name = globalName! };
        }
    }

    private static HashSet<string> CollectAssemblyPaths(string assemblyPath)
    {
        var dir = Path.GetDirectoryName(assemblyPath) ?? string.Empty;
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var dll in Directory.GetFiles(dir, "*.dll"))
            paths.Add(dll);

        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location);
        if (runtimeDir != null)
            foreach (var dll in Directory.GetFiles(runtimeDir, "*.dll"))
                paths.Add(dll);

        var acadApiLocation = typeof(CommandMethodAttribute).Assembly.Location;
        var acadDir = Path.GetDirectoryName(acadApiLocation);
        if (acadDir == null) return paths;
        {
            foreach (var dll in Directory.GetFiles(acadDir, "*.dll"))
                paths.Add(dll);
        }

        return paths;
    }

    private static IEnumerable<Type> GetMetadataTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null)!; }
    }
}
