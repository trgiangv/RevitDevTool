using DevTools.ModelsContext.Ports;

namespace DevTools.Presentation.Views.ModelsContext;

public sealed class SystemClock : IClock
{
    public DateTimeOffset Now => DateTimeOffset.UtcNow;
}
