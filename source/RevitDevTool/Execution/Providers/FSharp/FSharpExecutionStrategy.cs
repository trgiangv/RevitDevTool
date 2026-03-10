using System.Diagnostics;
using System.IO;
using Autodesk.Revit.UI;
using RevitDevTool.Controllers;
using RevitDevTool.Core;
using RevitDevTool.Execution.Interfaces;
using RevitDevTool.Execution.Models;
using RevitDevTool.Execution.Providers.Dotnet;
using RevitDevTool.Utils;
namespace RevitDevTool.Execution.Providers.FSharp;

/// <summary>
/// Execution strategy for F# scripts (.fsx).
/// Compiles via FSharpCompilationCache (with graph-level caching), executes like Dotnet Assembly.
/// </summary>
public sealed class FSharpExecutionStrategy(string scriptPath) : IExecutionStrategy
{
    private static readonly TimeSpan CompileTimeout = TimeSpan.FromSeconds(30);
    
    public async Task<ExecutionResult> ExecuteAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var scriptName = Path.GetFileName(scriptPath);
        try
        {
            progress?.Report($"Preparing {scriptName}...");
            var command = await CompileAsync(scriptPath, progress, cancellationToken).ConfigureAwait(false);
            if (command == null)
                return ExecutionResult.Failed($"F# compilation failed for '{scriptPath}'.", durationMs: stopwatch.ElapsedMilliseconds);

            progress?.Report($"Running {scriptName}...");
            var handler = await ExternalEventController
                .AsyncGenericEventHandler<ExecutionResult>()
                .ConfigureAwait(false);

            var result = await handler
                .RaiseAsync(_ =>
                {
                    var message = string.Empty;
                    var commandResult = FSharpExecutor.ExecuteCommand(command, CommandData.ExternalCommandData, ref message, CommandData.ElementSet);
                    stopwatch.Stop();
                    return commandResult.ToExecutionResult(message, stopwatch.ElapsedMilliseconds);
                })
                .ConfigureAwait(false);

            progress?.Report(result.Success
                ? $"Completed {scriptName}."
                : result.Message);

            return result;
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            stopwatch.Stop();
            return ExecutionResult.Cancelled("F# execution cancelled.", stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Trace.TraceError($"F# execution failed: {ex}");
            return ExecutionResult.Failed($"F# execution failed: {ex.Message}", ex, stopwatch.ElapsedMilliseconds);
        }
        finally
        {
            RevitContext.Application.PurgeReleasedAPIObjects();
        }
    }

    private static async Task<IExternalCommand?> CompileAsync(
        string scriptPath,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(CompileTimeout);

        try
        {
            return await FSharpCompilationCache.GetOrCompileAsync(scriptPath, progress, timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            Trace.TraceError($"F# compilation timeout after {CompileTimeout.TotalSeconds:0}s for '{scriptPath}'.");
            return null;
        }
    }
}
