using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace AcadDevTool.Adapters;

/// <summary>
/// AutoCAD thread dispatch: uses Document.LockDocument() to acquire API access
/// from a non-document thread. If already on the document thread, executes directly.
/// </summary>
public sealed class AcadHostContextExecutor : IHostContextExecutor
{
    public Task<T> ExecuteAsync<T>(Func<T> handler, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        T result;
        if (doc == null)
            result = handler();
        else
            using (doc.LockDocument())
                result = handler();

        return Task.FromResult(result);
    }

    public Task ExecuteAsync(Action action, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        if (doc == null)
            action();
        else
            using (doc.LockDocument())
                action();

        return Task.CompletedTask;
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> asyncHandler, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        if (doc == null)
            return await asyncHandler().ConfigureAwait(false);

        using (doc.LockDocument())
            return await asyncHandler().ConfigureAwait(false);
    }
}