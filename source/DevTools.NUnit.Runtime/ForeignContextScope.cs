namespace DevTools.NUnit.Runtime;

/// <summary>
/// NUnit can wait for an async test only on a context it knows how to pump. On any other context
/// it blocks the thread the test has to return to, and the host hangs. Another add-in can leave
/// such a context on the host's main thread, so it is taken off for the run and put back after.
/// </summary>
internal sealed class ForeignContextScope : IDisposable
{
    private readonly SynchronizationContext? _saved;
    private readonly bool _cleared;

    public ForeignContextScope()
    {
        var current = SynchronizationContext.Current;
        if (CanPump(current))
            return;

        _saved = current;
        _cleared = true;
        SynchronizationContext.SetSynchronizationContext(null);
    }

    public void Dispose()
    {
        if (_cleared)
            SynchronizationContext.SetSynchronizationContext(_saved);
    }

    internal static bool CanPump(SynchronizationContext? context) =>
        context is null
        || context.GetType() == typeof(SynchronizationContext)
        || context.GetType().FullName
            is "System.Windows.Forms.WindowsFormsSynchronizationContext"
            or "System.Windows.Threading.DispatcherSynchronizationContext";
}
