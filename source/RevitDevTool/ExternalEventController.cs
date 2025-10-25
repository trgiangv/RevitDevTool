using Nice3point.Revit.Toolkit.External.Handlers;

namespace RevitDevTool;

public static class ExternalEventController
{
    private static ActionEventHandler? _actionEventHandler;
    
    private const string HandlerNotSetMessage = "The handler was never set.";

    public static ActionEventHandler ActionEventHandler
    {
        get => _actionEventHandler ?? throw new InvalidOperationException(HandlerNotSetMessage);
        private set => _actionEventHandler = value;
    }

    public static void Register()
    {
        ActionEventHandler = new ActionEventHandler();
    }
}