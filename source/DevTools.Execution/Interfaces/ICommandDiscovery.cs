using DevTools.Execution.Providers.Dotnet;

namespace DevTools.Execution.Interfaces;

/// <summary>
/// Discovers executable commands from a .NET assembly.
/// Revit scans for IExternalCommand; AutoCAD scans for CommandMethodAttribute.
/// </summary>
public interface ICommandDiscovery
{
    List<CommandItem> ParseCommands(string assemblyPath);
}
