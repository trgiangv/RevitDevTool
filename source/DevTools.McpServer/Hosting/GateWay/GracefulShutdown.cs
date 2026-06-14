namespace DevTools.McpServer.Hosting.GateWay;

internal sealed class GracefulShutdown : IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    public CancellationToken Token => _cts.Token;

    public GracefulShutdown() => Console.CancelKeyPress += OnCancelKeyPress;

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true;
        _cts.Cancel();
    }

    public void Dispose()
    {
        Console.CancelKeyPress -= OnCancelKeyPress;
        _cts.Dispose();
    }
}
