using DevTools.ModelsContext.Ports;

namespace DevTools.ModelsContext.Views;

public sealed class SystemClock : IClock
{
    public DateTimeOffset Now => DateTimeOffset.UtcNow;
}
