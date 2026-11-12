using Autodesk.AutoCAD.Runtime;
using DevTools.AssemblyIsolation.Metadata;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Providers.Dotnet;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Reflection;
using ZLogger;

namespace AcadDevTool.Adapters;

/// <summary>
/// Discovers [CommandMethod] commands from .NET assemblies for AutoCAD.
/// Only public instance or static methods with void return and no parameters are considered.
/// <see cref="CommandItem.FullClassName"/> is stored as <c>TypeFullName.MethodName</c> so
/// <see cref="AcadCommandRunner"/> can split on the last dot into type and method (e.g.
/// <c>MyNs.MyClass.DoWork</c> → type <c>MyNs.MyClass</c>, method <c>DoWork</c>).
/// A type or method whose references cannot be resolved is skipped and logged; the rest are still listed.
/// </summary>
public sealed class AcadCommandDiscovery(ILogger<AcadCommandDiscovery> logger) : ICommandDiscovery
{
    private static readonly string CommandMethodFullName = typeof(CommandMethodAttribute).FullName!;
    private static readonly string VoidFullName = typeof(void).FullName!;
    private const string GlobalNameParameter = "globalName";
    private static readonly Type[] ResolutionAnchors =
    [
        typeof(CommandMethodAttribute),
        typeof(System.Windows.Window),
        typeof(System.Windows.Media.Visual),
        typeof(System.Windows.DependencyObject),
        typeof(AcadCommandDiscovery)
    ];
    private const BindingFlags CommandMethodFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;

    public List<CommandItem> ParseCommands(string assemblyPath)
    {
        if (!File.Exists(assemblyPath))
        {
            logger.ZLogError($"Assembly file not found: {assemblyPath}");
            return [];
        }

        var scan = new MetadataScan();
        var commands = ReadCommands(scan, assemblyPath);

        if (scan.Skipped.Count > 0)
            logger.ZLogWarning($"{scan.Skipped.Count} member(s) skipped in '{assemblyPath}' (unresolved references). First: {scan.Skipped[0]}");

        return commands;
    }

    private List<CommandItem> ReadCommands(MetadataScan scan, string assemblyPath)
    {
        try
        {
            using var session = MetadataAssemblySession.Create(assemblyPath, MetadataResolutionPaths.Collect(assemblyPath, ResolutionAnchors));
            return scan.GetTypes(session.LoadEntryAssembly())
                .Where(IsCandidateType)
                .SelectMany(type => FindCommands(scan, type, assemblyPath))
                .ToList();
        }
        catch (System.Exception ex)
        {
            logger.ZLogError($"Failed to parse commands from '{assemblyPath}': {ex.Message}");
            return [];
        }
    }

    private static IEnumerable<CommandItem> FindCommands(MetadataScan scan, Type type, string assemblyPath)
    {
        if (!scan.TryRead(type.FullName!, () => type.GetMethods(CommandMethodFlags), out var methods))
            return [];

        return methods
            .Select(method => ToCommand(scan, type, method, assemblyPath))
            .OfType<CommandItem>();
    }

    private static CommandItem? ToCommand(MetadataScan scan, Type type, MethodInfo method, string assemblyPath)
    {
        var fullName = $"{type.FullName}.{method.Name}";
        if (!scan.TryRead(fullName, () => GetCommandName(method), out var name) || name is null)
            return null;

        return new CommandItem(assemblyPath, fullName) { Name = name };
    }

    private static bool IsCandidateType(Type type)
    {
        if (type.IsInterface) return false;
        return type is not { IsAbstract: true, IsSealed: false };
    }

    private static string? GetCommandName(MethodInfo method)
    {
        var attribute = MetadataScan.FindAttribute(method, CommandMethodFullName);
        if (attribute is null) return null;

        var globalName = GetGlobalName(attribute);
        if (string.IsNullOrEmpty(globalName)) return null;

        if (method.ReturnType.FullName != VoidFullName) return null;
        if (method.GetParameters().Length != 0) return null;

        return globalName;
    }

    private static string? GetGlobalName(CustomAttributeData attribute)
    {
        var parameters = attribute.Constructor.GetParameters();
        for (var i = 0; i < parameters.Length && i < attribute.ConstructorArguments.Count; i++)
        {
            if (parameters[i].Name == GlobalNameParameter)
                return attribute.ConstructorArguments[i].Value as string;
        }

        return null;
    }
}
