using DevTools.Execution.Models;
using DevTools.Execution.Providers.Dotnet;

namespace DevTools.Execution.Interfaces;

/// <summary>
/// Executes a command item within the host's isolated context.
/// Revit: creates ExternalCommandData, calls IExternalCommand.Execute via CommandLoadContext.
/// AutoCAD: Activator.CreateInstance + MethodInfo.Invoke with document lock.
/// </summary>
public interface ICommandRunner
{
    ExecutionResult RunCommand(CommandItem commandItem);

    /// <summary>
    /// Executes a compiled F# command object using the host's execution mechanism.
    /// </summary>
    ExecutionResult RunFSharpCommand(object compiledCommand);
}
