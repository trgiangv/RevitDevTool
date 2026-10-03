using DevTools.Daemon.Auth;
using DevTools.Daemon.Mcp.Hosting;
using DevTools.Daemon.Mcp.Processes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZLogger;

namespace DevTools.Daemon.Gateway;

internal sealed class GatewayHostedService(
    IAuthService authService,
    McpEngine engine,
    IMcpPipeScanner pipeScanner,
    IOptions<GatewayOptions> gatewayOptions,
    ILoggerFactory loggerFactory,
    IServiceProvider appServices,
    ILogger<GatewayHostedService> logger) : BackgroundService, ITunnelStatusProvider
{
    private readonly SemaphoreSlim _tunnelGate = new(1, 1);
    private CancellationTokenSource? _tunnelCts;
    private Task? _tunnelTask;

    public TunnelStatus Status { get; private set; } = TunnelStatus.Disconnected;
    public event EventHandler<TunnelStatusChangedArgs>? StatusChanged;

    private void OnTunnelStatusChanged(object? sender, TunnelStatusChangedArgs args)
    {
        Status = args.Status;
        StatusChanged?.Invoke(this, args);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await authService.RefreshAsync().ConfigureAwait(false);

        authService.StateChanged += OnAuthStateChanged;

        if (authService.IsAuthenticated)
            StartTunnel(stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        authService.StateChanged -= OnAuthStateChanged;
        await StopTunnelAsync().ConfigureAwait(false);
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    private void OnAuthStateChanged(object? sender, AuthStateArgs args)
    {
        if (args.IsAuthenticated)
        {
            Task.Run(async () =>
            {
                await StopTunnelAsync().ConfigureAwait(false);
                StartTunnel(CancellationToken.None);
            });
        }
        else
        {
            Task.Run(StopTunnelAsync);
        }
    }

    private void StartTunnel(CancellationToken stoppingToken)
    {
        var url = gatewayOptions.Value.Url;
        if (string.IsNullOrEmpty(url))
        {
            logger.ZLogWarning($"Gateway URL not configured — tunnel disabled");
            return;
        }

        _tunnelCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var ct = _tunnelCts.Token;

        var options = McpServerFactory.CreateOptions(
            engine.ToolCollection, engine.PromptCollection, appServices);

        var tunnel = new GatewayTunnelClient(
            new Uri(url),
            async () =>
            {
                await authService.RefreshAsync().ConfigureAwait(false);
                return authService.AccessToken;
            },
            options,
            pipeScanner,
            loggerFactory,
            appServices,
            loggerFactory.CreateLogger<GatewayTunnelClient>());

        tunnel.StatusChanged += OnTunnelStatusChanged;

        _tunnelTask = Task.Run(async () =>
        {
            try
            {
                await tunnel.RunAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            finally
            {
                tunnel.StatusChanged -= OnTunnelStatusChanged;
                Status = TunnelStatus.Disconnected;
                StatusChanged?.Invoke(this, new TunnelStatusChangedArgs(TunnelStatus.Disconnected));
                await tunnel.DisposeAsync().ConfigureAwait(false);
            }
        }, ct);
    }

    private async Task StopTunnelAsync()
    {
        await _tunnelGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_tunnelCts is null)
                return;

            await _tunnelCts.CancelAsync().ConfigureAwait(false);

            if (_tunnelTask is not null)
            {
                try { await _tunnelTask.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
            }

            _tunnelCts.Dispose();
            _tunnelCts = null;
            _tunnelTask = null;
        }
        finally
        {
            _tunnelGate.Release();
        }
    }
}
