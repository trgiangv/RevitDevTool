namespace DevTools.ModelsContext.Panel;

internal sealed class RunningActions
{
    private bool _deferred;
    private int _running;

    public int Version { get; private set; }

    public void Begin()
    {
        _running++;
        Version++;
    }

    public bool End()
    {
        _running--;
        Version++;
        if (_running > 0 || !_deferred)
        {
            return false;
        }

        _deferred = false;
        return true;
    }

    public bool IsStale(int version)
    {
        return _running > 0 || version != Version;
    }

    public bool Defer()
    {
        if (_running == 0)
        {
            return false;
        }

        _deferred = true;
        return true;
    }
}
