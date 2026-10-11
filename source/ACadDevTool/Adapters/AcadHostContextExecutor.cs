using System.Runtime.ExceptionServices;
using System.Windows.Threading;
using Autodesk.Windows;
using DevTools.UI;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace AcadDevTool.Adapters;

/// <summary>
/// Runs a delegate on the AutoCAD UI thread. When a document is active and
/// <see cref="Autodesk.AutoCAD.ApplicationServices.DocumentCollection.IsApplicationContext"/>
/// is true, the delegate runs inside
/// <see cref="Autodesk.AutoCAD.ApplicationServices.DocumentCollection.ExecuteInCommandContextAsync"/>.
/// The returned task completes when that callback finishes. Cancelling the token
/// skips the handler if the callback has not started; it does not complete the task
/// while the handler is still running. <c>Document.LockDocument</c> is not a thread
/// or context switch.
/// </summary>
public sealed class AcadHostContextExecutor : IHostContextExecutor
{
    public Task<T> ExecuteAsync<T>(Func<T> handler, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(handler);
        token.ThrowIfCancellationRequested();

        var dispatcher = RequireDispatcher();
        if (dispatcher.CheckAccess())
            return RunCoreAsync(handler, token);

        return dispatcher
            .InvokeAsync(() => RunCoreAsync(handler, token), DispatcherPriority.Normal, token)
            .Task
            .Unwrap();
    }

    public Task ExecuteAsync(Action action, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        return ExecuteAsync(() =>
        {
            action();
            return true;
        }, token);
    }

    private static async Task<T> RunCoreAsync<T>(Func<T> handler, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var docs = AcadApp.DocumentManager;
        if (docs.MdiActiveDocument is null || !docs.IsApplicationContext)
            return handler();

        T result = default!;
        ExceptionDispatchInfo? failure = null;
        await docs.ExecuteInCommandContextAsync(_ =>
        {
            try
            {
                token.ThrowIfCancellationRequested();
                result = handler();
            }
            catch (Exception ex)
            {
                failure = ExceptionDispatchInfo.Capture(ex);
            }

            return Task.CompletedTask;
        }, null);

        failure?.Throw();
        return result;
    }

    private static Dispatcher RequireDispatcher()
    {
        if (HostUiHelper.HostDispatcher is { } dispatcher)
            return dispatcher;

        if (ComponentManager.Ribbon?.Dispatcher is { } ribbonDispatcher)
            return ribbonDispatcher;

        throw new InvalidOperationException("AutoCAD UI dispatcher is not initialized.");
    }
}
