using DevTools.Daemon.Auth;
using DevTools.Daemon.Mcp;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZLogger;

namespace DevTools.Daemon.Hosting;

/// <summary>
/// Manages the WebSocket tunnel to the MCP gateway relay.
/// Connects when authenticated; reconnects on auth state change.
/// </summary>
internal sealed class GatewayHostedService(
    IAuthService authService,
    McpEngine engine,
    IOptions<GatewayOptions> gatewayOptions,
    ILoggerFactory loggerFactory,
    ILogger<GatewayHostedService> logger) : BackgroundService, ITunnelStatusProvider
{
    private CancellationTokenSource? _tunnelCts;
    private Task? _tunnelTask;
    private GatewayTunnelClient? _currentTunnel;

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

    private void OnAuthStateChanged(object? sender, AuthStateChangedArgs args)
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

        var options = ToolHelpers.ConfigureGatewayOptions(
            engine.ToolCollection, engine.PromptCollection, engine.ResourceCollection);

        var tunnel = new GatewayTunnelClient(
            new Uri(url),
            async () =>
            {
                await authService.RefreshAsync().ConfigureAwait(false);
                return authService.AccessToken;
            },
            options,
            loggerFactory,
            loggerFactory.CreateLogger<GatewayTunnelClient>());

        tunnel.StatusChanged += OnTunnelStatusChanged;
        _currentTunnel = tunnel;

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
                _currentTunnel = null;
                Status = TunnelStatus.Disconnected;
                StatusChanged?.Invoke(this, new TunnelStatusChangedArgs(TunnelStatus.Disconnected));
                await tunnel.DisposeAsync().ConfigureAwait(false);
            }
        }, ct);
    }

    private async Task StopTunnelAsync()
    {
        if (_tunnelCts is not null)
        {
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
    }
}
