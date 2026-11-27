namespace DevTools.ModelsContext.Panel;

internal sealed class ActionVersion
{
    private bool _dropped;
    private int _running;

    public int Value { get; private set; }

    public void Begin()
    {
        _running++;
        Value++;
    }

    public bool End()
    {
        _running--;
        Value++;
        if (_running > 0 || !_dropped)
        {
            return false;
        }

        _dropped = false;
        return true;
    }

    public bool IsStale(int value)
    {
        return _running > 0 || value != Value;
    }

    public bool Drop()
    {
        if (_running == 0)
        {
            return true;
        }

        _dropped = true;
        return false;
    }
}
