using System.Collections.Concurrent;
using Autodesk.Revit.UI;
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
    
    public static AsyncEventHandler<T> AsyncGenericEventHandler<T>()
    {
        IExternalEventHandler handler = null!;
        
        if (AsyncGenericHandlers.TryGetValue(typeof(T), out var existing))
            return (AsyncEventHandler<T>)existing;
        
        if (Context.IsRevitInApiMode)
        {
            handler = AsyncGenericHandlers.GetOrAdd(
                typeof(T),
                _ => new AsyncEventHandler<T>());

            return (AsyncEventHandler<T>)handler;
        }
        
        AsyncEventHandler.RaiseAsync(_ =>
        {
            handler = AsyncGenericHandlers.GetOrAdd(
                typeof(T),
                _ => new AsyncEventHandler<T>());
        }).GetAwaiter().GetResult();

        return (AsyncEventHandler<T>)handler;
    }

    public static void Register()
    {
        if (_isRegistered) return;
        _actionHandler = new ActionEventHandler();
        _asyncHandler = new AsyncEventHandler();
        _isRegistered = true;
    }
}