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