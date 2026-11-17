namespace DevTools.ModelsContext.Engine;

public sealed class SlicePace(int firstBatch, int minBatch, int maxBatch)
{
    private double _ticksPerItem;

    public int Next(TimeSpan remaining)
    {
        if (_ticksPerItem <= 0)
        {
            return firstBatch;
        }

        var fits = remaining.Ticks / _ticksPerItem;
        return fits >= maxBatch ? maxBatch : Math.Max(minBatch, (int)fits);
    }

    public void Record(int items, TimeSpan elapsed)
    {
        if (items <= 0)
        {
            return;
        }

        var measured = Math.Max(1.0, elapsed.Ticks) / items;
        _ticksPerItem = _ticksPerItem <= 0 ? measured : (_ticksPerItem + measured) / 2;
    }
}
