using System.Diagnostics;
using RevitDevTool.Controllers;
using RevitDevTool.Core;
using RevitDevTool.Execution.Interfaces;
using RevitDevTool.Execution.Models;
using RevitDevTool.Utils;
namespace RevitDevTool.Execution.Providers.Dotnet;

/// <summary>
/// Execution strategy for .NET commands.
/// </summary>
public sealed class AssemblyExecutionStrategy(AddinItem addinItem) : IExecutionStrategy
{
    public async Task<ExecutionResult> ExecuteAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
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
                    return commandResult.ToExecutionResult(message, stopwatch.ElapsedMilliseconds);
                })
                .ConfigureAwait(false);

            progress?.Report(result.Success
                ? $"Completed {addinItem.Name}."
                : result.Message);

            return result;
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            stopwatch.Stop();
            return ExecutionResult.Cancelled("Dotnet execution cancelled.", stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Trace.TraceError($"Dotnet execution failed: {ex}");
            return ExecutionResult.Failed($"Dotnet execution failed: {ex.Message}", ex, stopwatch.ElapsedMilliseconds);
        }
        finally
        {
            RevitContext.Application.PurgeReleasedAPIObjects();
        }
    }
}