using DevTools.Presentation.Interfaces;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace AcadDevTool.Bridges;

public sealed class AcadIdlingBridge : IHostIdlingBridge
{
    private readonly List<Action> _callbacks = [];
    private EventHandler? _handler;

    public void Subscribe(Action callback)
    {
        _callbacks.Add(callback);
        if (_handler != null) return;
        _handler = (_, _) => { foreach (var cb in _callbacks) cb(); };
        AcadApp.Idle += _handler;
    }

    public void Unsubscribe(Action callback)
    {
        _callbacks.Remove(callback);
        if (_callbacks.Count != 0 || _handler == null) return;
        AcadApp.Idle -= _handler;
        _handler = null;
    }

    public void Dispose()
    {
        if (_handler != null)
        {
            AcadApp.Idle -= _handler;
            _handler = null;
        }
        _callbacks.Clear();
    }
}
