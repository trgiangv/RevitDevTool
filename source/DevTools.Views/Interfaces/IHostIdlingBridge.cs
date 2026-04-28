namespace DevTools.Views.Interfaces;

public interface IHostIdlingBridge : IDisposable
{
    void Subscribe(Action callback);
    void Unsubscribe(Action callback);
}
