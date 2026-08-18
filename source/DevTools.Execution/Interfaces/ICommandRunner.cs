using DevTools.Execution.Models;
using DevTools.Execution.Providers.Dotnet;

namespace DevTools.Execution.Interfaces;

/// <summary>
/// Executes a command item within the host's isolated context.
/// Revit: creates ExternalCommandData and calls IExternalCommand.Execute in a collectible isolation session.
/// AutoCAD: Activator.CreateInstance + MethodInfo.Invoke with document lock.
/// </summary>
public interface ICommandRunner
{
    ExecutionResult RunCommand(CommandItem commandItem);

    /// <summary>
    /// Executes a compiled script command (C# or F#) using the host's execution mechanism.
    /// </summary>
    ExecutionResult RunCompiledCommand(object compiledCommand);
}
