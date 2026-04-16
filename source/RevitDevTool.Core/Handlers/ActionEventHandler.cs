using System.ComponentModel;
// ReSharper disable once CheckNamespace
namespace RevitDevTool.Core;

/// <summary>
///     Handler, to provide access to modify the Revit document with the ability to queue calls to Raise methods.
/// </summary>
[PublicAPI]
public class ActionEventHandler : ExternalEventHandler
{
    private Action<UIApplication>? _contextAction;
    private Action? _action;

    /// <summary>Callback invoked by Revit. Not used to be called in user code.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override void Execute(UIApplication uiApplication)
    {
        if (_contextAction is null && _action is null) return;

        try
        {
            _contextAction?.Invoke(uiApplication);
            _action?.Invoke();
        }
        finally
        {
            _contextAction = null;
            _action = null;
        }
    }

    /// <summary>
    ///     Instructing Revit to queue a handler and raise (signal) the external event.
    /// </summary>
    /// <remarks>
    ///     Revit will wait until it is ready to process the event and then
    ///     it will execute its event handler by calling the Execute method.
    ///     Revit processes external events only when no other commands or
    ///     edit modes are currently active in Revit, which is the same policy
    ///     like the one that applies to evoking external commands.<br/><br/>
    ///     Executes the handler out of queue if Revit is in API mode.
    /// </remarks>
    public void Raise(Action<UIApplication> action)
    {
        if (RevitContext.IsRevitInApiMode)
        {
            action(RevitContext.UiApplication);
            return;
        }
        
        if (_contextAction is null) _contextAction = action;
        else _contextAction += action;

        Raise();
    }
    
    /// <summary>
    ///     Instructing Revit to queue a handler and raise (signal) the external event.
    /// </summary>
    /// <remarks>
    ///     Revit will wait until it is ready to process the event and then
    ///     it will execute its event handler by calling the Execute method.
    ///     Revit processes external events only when no other commands or
    ///     edit modes are currently active in Revit, which is the same policy
    ///     like the one that applies to evoking external commands.<br/><br/>
    ///     Executes the handler out of queue if Revit is in API mode.
    /// </remarks>
    public void Raise(Action action)
    {
        if (RevitContext.IsRevitInApiMode)
        {
            action();
            return;
        }
        
        if (_action is null) _action = action;
        else _action += action;

        Raise();
    }

    /// <summary>
    ///     Clears the call queue of subscribed delegates.
    /// </summary>
    /// <remarks>The queue can be cleaned up before the first delegate is invoked.</remarks>
    public void Cancel()
    {
        _contextAction = null;
    }
}