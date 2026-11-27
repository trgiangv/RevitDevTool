using System.Globalization;

namespace DevTools.ModelsContext.Panel;

public readonly record struct PanelActivity(bool IsIdle, int Generation)
{
    public override string ToString()
    {
        return (IsIdle ? "Idle " : "Busy ") + Generation.ToString(CultureInfo.InvariantCulture);
    }
}
