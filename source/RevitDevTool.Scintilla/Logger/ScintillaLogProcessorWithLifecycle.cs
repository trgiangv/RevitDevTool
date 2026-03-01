using ZLogger;
namespace RevitDevTool.Scintilla.Logger;

/// <summary>
/// Wraps <see cref="ScintillaLogProcessor"/> and runs an unbind action on dispose,
/// allowing the ZLogger-owned provider to clean up event bindings when the logging
/// infrastructure shuts down.
/// </summary>
internal sealed class ScintillaLogProcessorWithLifecycle : IAsyncLogProcessor
{
    private readonly ScintillaLogProcessor _inner;
    private readonly Action? _onDispose;
    private int _disposed;

    public ScintillaLogProcessorWithLifecycle(ScintillaLogProcessor inner, Action? onDispose)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _onDispose = onDispose;
    }

    public void Post(IZLoggerEntry log) => _inner.Post(log);

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _onDispose?.Invoke();
        await _inner.DisposeAsync().ConfigureAwait(false);
    }
}
