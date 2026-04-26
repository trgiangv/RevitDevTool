using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ZLogger;
using DevTools.McpParser.Models;

namespace DevTools.McpServer;

public sealed partial class InstanceManager(ILogger<InstanceManager> logger) : IAsyncDisposable
{
    [GeneratedRegex(@"^Revit_\d{4}_\d+$", RegexOptions.IgnoreCase)]
    private static partial Regex RevitPipePattern();

    private readonly ConcurrentDictionary<string, RevitBridgeClient> _clients = new(StringComparer.OrdinalIgnoreCase);

    public event Action? Changed;

    public List<RevitBridgeClient> GetClients() => _clients.Values.ToList();

    public List<InstanceInfo> GetInstances() =>
        _clients.Values
            .Where(c => c.Info is not null)
            .Select(c => c.Info!)
            .ToList();

    public RevitBridgeClient? GetByProcessId(int processId) =>
        _clients.Values.FirstOrDefault(c => c.Info?.ProcessId == processId);

    public RevitBridgeClient? GetDefault() =>
        _clients.Count == 1 ? _clients.Values.First() : null;

    public async Task RunDiscoveryAsync(CancellationToken ct)
    {
        var knownPipes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await SyncPipesAsync(knownPipes, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.ZLogError(ex, $"Discovery error");
            }

            try
            {
                await Task.Delay(2000, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private async Task SyncPipesAsync(HashSet<string> knownPipes, CancellationToken ct)
    {
        var currentPipes = DiscoverRevitPipes(logger);

        foreach (var pipeName in knownPipes.Where(p => !currentPipes.Contains(p)).ToList())
        {
            knownPipes.Remove(pipeName);
            await DisconnectAsync(pipeName).ConfigureAwait(false);
        }

        foreach (var pipeName in currentPipes.Where(p => !knownPipes.Contains(p)).ToList())
        {
            knownPipes.Add(pipeName);
            await TryConnectAsync(pipeName, ct).ConfigureAwait(false);
        }
    }

    private async Task TryConnectAsync(string pipeName, CancellationToken ct)
    {
        try
        {
            logger.ZLogInformation($"Connecting to {pipeName}...");
            var client = await RevitBridgeClient.ConnectAsync(pipeName, ct).ConfigureAwait(false);
            client.ToolsChanged += () => Changed?.Invoke();
            client.DocumentChanged += _ => Changed?.Invoke();
            client.Disconnected += () =>
            {
                _ = DisconnectAsync(pipeName);
                Changed?.Invoke();
            };

            _clients[pipeName] = client;
            logger.ZLogInformation($"Connected to {pipeName} (PID={client.Info?.ProcessId}, Doc={client.Info?.DocumentTitle})");

            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            logger.ZLogWarning(ex, $"Failed to connect to {pipeName}");
        }
    }

    private async Task DisconnectAsync(string pipeName)
    {
        if (_clients.TryRemove(pipeName, out var client))
        {
            await client.DisposeAsync().ConfigureAwait(false);
            logger.ZLogInformation($"Disconnected from {pipeName}");
        }
    }

    public static HashSet<string> DiscoverRevitPipes(ILogger? logger = null)
    {
        var pipes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var path in Directory.GetFiles(@"\\.\pipe\"))
            {
                var name = Path.GetFileName(path);
                if (IsRevitEntryPipe(name))
                    pipes.Add(name);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger?.ZLogWarning(ex, $"Pipe scan error");
        }

        return pipes;
    }

    public static int ParseProcessId(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number)
            return element.GetInt32();

        return int.TryParse(element.GetString(), out var pid) ? pid : 0;
    }

    private static bool IsRevitEntryPipe(string name) => RevitPipePattern().IsMatch(name);

    public async ValueTask DisposeAsync()
    {
        foreach (var kvp in _clients.ToArray())
        {
            if (_clients.TryRemove(kvp.Key, out var client))
                await client.DisposeAsync().ConfigureAwait(false);
        }
    }
}
