using System.Collections;

namespace RevitDevTool.Handlers;

public static class ExternalEventController
{
    private static ActionEventHandler? _actionEventHandler;
    private static AsyncEventHandler? _asyncEventHandler;
    private static AsyncEventHandler<IEnumerable>? _asyncCollectionEventHandler;
    private static IdlingEventHandler? _idlingEventHandler;
    
    private const string HandlerNotSetMessage = "The handler was never set.";

    public static ActionEventHandler ActionEventHandler
    {
        get => _actionEventHandler ?? throw new InvalidOperationException(HandlerNotSetMessage);
        private set => _actionEventHandler = value;
    }

    public static AsyncEventHandler AsyncEventHandler
    {
        get => _asyncEventHandler ?? throw new InvalidOperationException(HandlerNotSetMessage);
        private set => _asyncEventHandler = value;
    }

    public static AsyncEventHandler<IEnumerable> AsyncCollectionEventHandler
    {
        get => _asyncCollectionEventHandler ?? throw new InvalidOperationException(HandlerNotSetMessage);
        private set => _asyncCollectionEventHandler = value;
    }
    
    public static IdlingEventHandler IdlingEventHandler
    {
        get => _idlingEventHandler ?? throw new InvalidOperationException(HandlerNotSetMessage);
        private set => _idlingEventHandler = value;
    }

    public static void Register()
    {
        ActionEventHandler = new ActionEventHandler();
        AsyncEventHandler = new AsyncEventHandler();
        AsyncCollectionEventHandler = new AsyncEventHandler<IEnumerable>();
        IdlingEventHandler = new IdlingEventHandler();
    }
}