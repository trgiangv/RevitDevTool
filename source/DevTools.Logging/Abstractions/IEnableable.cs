namespace DevTools.Logging.Abstractions;

public interface IEnableable
{
    bool IsEnabled { get; }
    void Enable<T>(T options);
    void Disable();
}
