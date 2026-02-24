using System.Diagnostics;
using System.IO;
using Autodesk.Revit.UI;
using RevitDevTool.Bridge;
using RevitDevTool.CodeExecute.Providers.DotNet;
using RevitDevTool.CodeExecute.Providers.Python;

namespace RevitDevTool.Controllers;

/// <summary>
/// Orchestrates job execution: open document → run script → close document.
/// </summary>
public static class JobController
{
    public static async Task<JobResult> ExecuteAsync(ResolvedJob job)
    {
        var prepResult = await PreparePythonIfNeededAsync(job).ConfigureAwait(false);
        if (prepResult != null) return prepResult;

        return await ExternalEventController.AsyncGenericEventHandler<JobResult>().RaiseAsync(app =>
        {
            var sw = Stopwatch.StartNew();
            Document? openedDoc = null;
            try
            {
                openedDoc = TryOpenDocument(app, job);
                RunScript(job, openedDoc);
                sw.Stop();
                return JobResult.Ok(sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                sw.Stop();
                Trace.TraceError($"[JobController] Job failed: {ex.Message}");
                return JobResult.Fail(ex, sw.ElapsedMilliseconds);
            }
            finally
            {
                if (job.Lifecycle.CloseDocument && openedDoc is { IsValidObject: true })
                    DocumentController.Close(app, job, openedDoc);
            }
        }).ConfigureAwait(true);
    }

    private static async Task<JobResult?> PreparePythonIfNeededAsync(ResolvedJob job)
    {
        if (string.IsNullOrEmpty(job.Script) || !job.Script.EndsWith(".py", StringComparison.OrdinalIgnoreCase))
            return null;

        await PythonInitializer.InitializeAsync().ConfigureAwait(false);

        var resolved = await PythonExecutionStrategy.ResolveDependenciesAsync(job.Script).ConfigureAwait(false);

        if (resolved) return null;

        return new JobResult
        {
            Success = false,
            Error = "Dependency resolution failed or was cancelled by user.",
            DurationMs = 0
        };
    }

    private static Document? TryOpenDocument(UIApplication uiApp, ResolvedJob job)
    {
        if (string.IsNullOrEmpty(job.FilePath) || !File.Exists(job.FilePath))
            return null;

        var doc = DocumentController.Open(uiApp, job);
        Trace.TraceInformation($"[JobController] Opened document ({(job.Open.Headless ? "headless" : "UI")}): {job.FilePath}");
        return doc;
    }

    private static void RunScript(ResolvedJob job, Document? document)
    {
        if (string.IsNullOrEmpty(job.Script))
            return;

        var injectDoc = job.Open.Headless ? document : null;

        if (job.Script.EndsWith(".py", StringComparison.OrdinalIgnoreCase))
        {
            var content = File.ReadAllText(job.Script);
            var rootFolder = Path.GetDirectoryName(job.Script) ?? "";
            PythonExecutor.ExecuteScript(job.Script, content, rootFolder, throwOnError: true, document: injectDoc);
        }
        else if (job.Script.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            var items = AddinLoaderService.ParseCommands(job.Script);
            if (items.Count <= 0) return;
            var message = "";
            AddinExecutor.RunCommand(items[0], AddinLoadHelper.ExternalCommandData, ref message, AddinLoadHelper.ElementSet);
        }
        else
        {
            throw new NotSupportedException($"Unsupported script type: {Path.GetExtension(job.Script)}");
        }
    }
}
