using Autodesk.Revit.UI;

namespace RevitDevTool.Revit.Command;

public sealed class TraceGeometryEventHandler : IExternalEventHandler
{
    private Queue<Action<UIApplication>> Actions { get; set; }
    private static readonly Lazy<TraceGeometryEventHandler> Lazy = new(() => new TraceGeometryEventHandler());
    private ExternalEvent _externalEvent;
    public static TraceGeometryEventHandler Instance => Lazy.Value;

    private TraceGeometryEventHandler()
    {
        Actions = new Queue<Action<UIApplication>>();
    }

    public void Invoke(Action<UIApplication> action)
    {
        Actions.Enqueue(action);
        _externalEvent.Raise();
    }

    public void Execute(UIApplication app)
    {
        while (Actions.Count != 0)
        {
            var action = Actions.Dequeue();
            action.Invoke(app);
        }
    }

    public string GetName()
    {
        return "TraceGeometry";
    }

    public void Start()
    {
        _externalEvent ??= ExternalEvent.Create(Instance);
    }
}