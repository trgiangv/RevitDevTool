using DevTools.ModelsContext.Panel;

namespace DevTools.Presentation.Views.ModelsContext.Panel;

public sealed class SystemClock : IClock
{
    public DateTimeOffset Now => DateTimeOffset.UtcNow;
}
