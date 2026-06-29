namespace DevTools.Logging.Abstractions;

public interface IActivatable
{
    void Enable<T>(T options);
    void Disable();
}
