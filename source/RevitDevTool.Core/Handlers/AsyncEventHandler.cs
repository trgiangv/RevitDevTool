using System.ComponentModel;

// ReSharper disable once CheckNamespace
namespace RevitDevTool.Core;

/// <summary>
///     Handler, to provide access to modify the Revit document.
/// </summary>
/// <remarks>Suitable for cases where it is needed to await the completion of an external event.</remarks>
[PublicAPI]
public sealed class AsyncEventHandler : ExternalEventHandler
{
    private Action<UIApplication>? _contextAction;
    private Action? _action;
    private TaskCompletionSource? _resultTask;

    /// <summary>Callback invoked by Revit. Not used to be called in user code.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override void Execute(UIApplication uiApplication)
    {
        if (_contextAction is null && _action is null) return;
        if (_resultTask is null) return;
        
        try
        {
            _contextAction?.Invoke(uiApplication);
            _action?.Invoke();
            _resultTask.SetResult();
        }
        catch (Exception exception)
        {
            _resultTask.SetException(exception);
        }
        finally
        {
            _contextAction = null;
            _resultTask = null;
        }
    }

    /// <summary>
    ///     Instructing Revit to queue a handler, raise (signal) the external event and async awaiting for its completion.
    /// </summary>
    /// <remarks>
    ///     This method async awaiting completion of the <see cref="Execute" /> method. <br />
    ///     Exceptions in the delegate will not be ignored and will be rethrown in the original synchronization context.<br />
    ///     <see cref="System.Threading.Tasks.Task.WaitAll(System.Threading.Tasks.Task[])" />,
    ///     <see cref="System.Threading.Tasks.Task.Wait()" /> will cause a deadlock.<br/><br/>
    ///     Executes the handler out of queue if Revit is in API mode.
    /// </remarks>
    public async Task RaiseAsync(Action<UIApplication> action)
    {
        if (RevitContext.IsRevitInApiMode)
        {
            action.Invoke(RevitContext.UiApplication);
            return;
        }

        if (_contextAction is null) _contextAction = action;
        else _contextAction += action;
        _resultTask ??= new TaskCompletionSource();
        Raise();
        await _resultTask.Task;
    }
    
    /// <summary>
    ///     Instructing Revit to queue a handler, raise (signal) the external event and async awaiting for its completion.
    /// </summary>
    /// <remarks>
    ///     This method async awaiting completion of the <see cref="Execute" /> method. <br />
    ///     Exceptions in the delegate will not be ignored and will be rethrown in the original synchronization context.<br />
    ///     <see cref="System.Threading.Tasks.Task.WaitAll(System.Threading.Tasks.Task[])" />,
    ///     <see cref="System.Threading.Tasks.Task.Wait()" /> will cause a deadlock.<br/><br/>
    ///     Executes the handler out of queue if Revit is in API mode.
    /// </remarks>
    public async Task RaiseAsync(Action action)
    {
        if (RevitContext.IsRevitInApiMode)
        {
            action.Invoke();
            return;
        }

        if (_action is null) _action = action;
        else _action += action;
        _resultTask ??= new TaskCompletionSource();
        Raise();
        await _resultTask.Task;
    }
}