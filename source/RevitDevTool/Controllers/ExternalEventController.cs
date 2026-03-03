using System.Collections.Concurrent;
using System.ComponentModel;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;
using Nice3point.Revit.Toolkit.External.Handlers;
// ReSharper disable ReplaceWithFieldKeyword
namespace RevitDevTool.Controllers;

[PublicAPI]
public static class ExternalEventController
{
    private const string HandlerNotSetMessage = "The handler was never set.";
    private static bool _isRegistered;
    
    private static ActionEventHandler? _actionHandler;
    private static AsyncEventHandler? _asyncHandler;
    private static readonly ConcurrentDictionary<Type, IExternalEventHandler> AsyncGenericHandlers = new();
    
    public static ActionEventHandler ActionEventHandler =>
        _actionHandler ?? throw new InvalidOperationException(HandlerNotSetMessage);

    public static AsyncEventHandler AsyncEventHandler =>
        _asyncHandler ?? throw new InvalidOperationException(HandlerNotSetMessage);
    
    public static async Task<AsyncEventHandler<T>> AsyncGenericEventHandler<T>()
    {
        if (AsyncGenericHandlers.TryGetValue(typeof(T), out var existing))
            return (AsyncEventHandler<T>)existing;

        if (Context.IsRevitInApiMode)
        {
            var handler = AsyncGenericHandlers.GetOrAdd(
                typeof(T),
                _ => new AsyncEventHandler<T>());

            return (AsyncEventHandler<T>)handler;
        }

        IExternalEventHandler? initializedHandler = null;
        await AsyncEventHandler.RaiseAsync(_ =>
        {
            initializedHandler = AsyncGenericHandlers.GetOrAdd(
                typeof(T),
                _ => new AsyncEventHandler<T>());
        }).ConfigureAwait(false);

        return (AsyncEventHandler<T>)initializedHandler!;
    }

    public static void Register()
    {
        if (_isRegistered) return;
        _actionHandler = new ActionEventHandler();
        _asyncHandler = new AsyncEventHandler();
        _isRegistered = true;
    }
}

/// <summary>
///     Handler, to provide access to modify the Revit document with the ability to queue calls to Raise methods.
/// </summary>
[PublicAPI]
public class ActionEventHandler : ExternalEventHandler
{
    private Action<UIApplication>? _action;
    private Action? _simpleAction;

    /// <summary>Callback invoked by Revit. Not used to be called in user code.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override void Execute(UIApplication uiApplication)
    {
        if (_action is null && _simpleAction is null) return;

        try
        {
            _action?.Invoke(uiApplication);
            _simpleAction?.Invoke();
        }
        finally
        {
            _action = null;
            _simpleAction = null;
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
        if (Context.IsRevitInApiMode)
        {
            action(Context.UiApplication);
            return;
        }
        
        if (_action is null) _action = action;
        else _action += action;

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
        if (Context.IsRevitInApiMode)
        {
            action();
            return;
        }
        
        if (_simpleAction is null) _simpleAction = action;
        else _simpleAction += action;

        Raise();
    }

    /// <summary>
    ///     Clears the call queue of subscribed delegates.
    /// </summary>
    /// <remarks>The queue can be cleaned up before the first delegate is invoked.</remarks>
    public void Cancel()
    {
        _action = null;
    }
}