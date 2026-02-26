using RevitDevTool.CodeExecute.Interfaces;
using RevitDevTool.CodeExecute.Models;
using RevitDevTool.Controllers;
using System.Diagnostics;
namespace RevitDevTool.CodeExecute.Providers.Dotnet;

/// <summary>
/// Execution strategy for .NET commands.
/// </summary>
public sealed class AssemblyExecutionStrategy(AddinItem addinItem) : IExecutionStrategy
{
    public async Task<ExecutionResult> ExecuteAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        progress?.Report($"Running {addinItem.Name}...");

        var handler = await ExternalEventController
            .AsyncGenericEventHandler<ExecutionResult>()
            .ConfigureAwait(false);

        var result = await handler
            .RaiseAsync(_ =>
            {
                var message = string.Empty;
                var commandResult = AddinExecutor.RunCommand(addinItem, AddinCommandData.ExternalCommandData, ref message, AddinCommandData.ElementSet);
                stopwatch.Stop();
                return MapRevitResult(commandResult, message, stopwatch.ElapsedMilliseconds);
            })
            .ConfigureAwait(false);

        progress?.Report(result.Success
            ? $"Completed {addinItem.Name}."
            : result.Message);

        return result;
    }

    private static ExecutionResult MapRevitResult(Autodesk.Revit.UI.Result result, string message, long durationMs)
    {
        return result switch
        {
            Autodesk.Revit.UI.Result.Succeeded => ExecutionResult.Succeeded("Command completed successfully.", durationMs),
            Autodesk.Revit.UI.Result.Cancelled => ExecutionResult.Cancelled(
                string.IsNullOrWhiteSpace(message) ? "Command cancelled." : message,
                durationMs),
            _ => ExecutionResult.Failed(
                string.IsNullOrWhiteSpace(message) ? "Command failed." : message,
                durationMs: durationMs)
        };
    }
}