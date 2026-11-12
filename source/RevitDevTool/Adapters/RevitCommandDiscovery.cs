using DevTools.AssemblyIsolation.Metadata;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Providers.Dotnet;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Reflection;
using ZLogger;
namespace RevitDevTool.Adapters;

public sealed class RevitCommandDiscovery(ILogger<RevitCommandDiscovery> logger) : ICommandDiscovery
{
    private static readonly string CommandFullName = typeof(IExternalCommand).FullName!;
    private static readonly Type[] ResolutionAnchors =
    [
        typeof(IExternalCommand),
        typeof(System.Windows.Window),
        typeof(System.Windows.Media.Visual),
        typeof(System.Windows.DependencyObject),
        typeof(RevitCommandDiscovery)
    ];

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
            logger.ZLogWarning($"{scan.Skipped.Count} type(s) skipped in '{assemblyPath}' (unresolved references). First: {scan.Skipped[0]}");

        return commands;
    }

    private List<CommandItem> ReadCommands(MetadataScan scan, string assemblyPath)
    {
        try
        {
            using var session = MetadataAssemblySession.Create(assemblyPath, MetadataResolutionPaths.Collect(assemblyPath, ResolutionAnchors));
            return scan.GetTypes(session.LoadEntryAssembly())
                .Where(type => IsCommand(scan, type))
                .Select(type => new CommandItem(assemblyPath, type.FullName!))
                .ToList();
        }
        catch (Exception ex)
        {
            logger.ZLogError($"Failed to parse commands from '{assemblyPath}': {ex.Message}");
            return [];
        }
    }

    private static bool IsCommand(MetadataScan scan, Type type)
    {
        if (type.IsAbstract || type.IsInterface) return false;
        return scan.TryRead(type.FullName!, () => ImplementsInterface(type, CommandFullName), out var isCommand) && isCommand;
    }

    private static bool ImplementsInterface(Type type, string interfaceFullName)
    {
        return type.GetInterfaces().Any(i =>
            i.FullName != null && i.FullName == interfaceFullName);
    }
}
