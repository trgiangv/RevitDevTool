using Autodesk.Revit.UI.Events;
using DevTools.Presentation.Interfaces;
using RevitDevTool.Core;

namespace RevitDevTool.Bridges;

public sealed class RevitIdlingBridge : IHostIdlingBridge
{
    private readonly List<Action> _callbacks = [];
    private EventHandler<IdlingEventArgs>? _handler;

    public void Subscribe(Action callback)
    {
        _callbacks.Add(callback);
        if (_handler != null) return;
        _handler = (_, _) => { foreach (var cb in _callbacks) cb(); };
        RevitContext.UiApplication.Idling += _handler;
    }

    public void Unsubscribe(Action callback)
    {
        _callbacks.Remove(callback);
        if (_callbacks.Count != 0 || _handler == null) return;
        RevitContext.UiApplication.Idling -= _handler;
        _handler = null;
    }

    public void Dispose()
    {
        if (_handler != null)
        {
            RevitContext.UiApplication.Idling -= _handler;
            _handler = null;
        }
        _callbacks.Clear();
    }
}
