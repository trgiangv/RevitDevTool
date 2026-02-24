using Autodesk.Revit.UI;
using RevitDevTool.CodeExecute.Interfaces;
using RevitDevTool.CodeExecute.Providers.DotNet;
using RevitDevTool.Controllers;
using System.Diagnostics;

namespace RevitDevTool.CodeExecute.Providers.FSharp;

/// <summary>
/// Execution strategy for F# scripts (.fsx).
/// Compiles via FsiEvaluationSession, finds IExternalCommand, executes like DotNet.
/// </summary>
public sealed class FSharpExecutionStrategy(string scriptPath) : IExecutionStrategy
{
    private static readonly TimeSpan CompileTimeout = TimeSpan.FromSeconds(30);

    public void Execute()
    {
        ExecuteAsync().ConfigureAwait(false);
    }

    private static async Task<IExternalCommand?> CompileAsync(string scriptPath)
    {
        var compileTask = Task.Run(() => FSharpExecutor.CompileScript(scriptPath));
        var timeoutTask = Task.Delay(CompileTimeout);
        var completedTask = await Task.WhenAny(compileTask, timeoutTask).ConfigureAwait(false);

        if (completedTask == compileTask) return await compileTask.ConfigureAwait(false);
        Trace.TraceError($"F# compilation timeout after {CompileTimeout.TotalSeconds:0}s for '{scriptPath}'.");
        return null;
    }

    private async Task ExecuteAsync()
    {
        try
        {
            var command = await CompileAsync(scriptPath).ConfigureAwait(false);
            if (command == null)
                return;

            var message = string.Empty;
            ExternalEventController.ActionEventHandler.Raise(_ =>
            {
                var result = FSharpExecutor.ExecuteCommand(
                    command,
                    AddinLoadHelper.ExternalCommandData,
                    ref message,
                    AddinLoadHelper.ElementSet);

                if (result != Result.Succeeded)
                    Trace.TraceWarning($"F# command returned {result}.");
            });
        }
        catch (Exception ex)
        {
            Trace.TraceError($"F# async execution failed: {ex}");
        }
    }
}
