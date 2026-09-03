using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows.Threading;
using DevTools.Mcp.Catalog;
using DevTools.Presentation.Services;
using DevTools.Settings;
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace DevTools.Presentation.ViewModels.Settings;

public sealed partial class McpSettingViewModel : ObservableObject, IDisposable
{
    private const string StatusRunning = "Daemon: Running";
    private const string StatusNotRunning = "Daemon: Not running";
    private const string StatusUnknown = "Daemon: Unknown";
    private const int SignInTimeoutSeconds = 120;

    private readonly ISettingsService _settingsService;
    private readonly McpCatalogStore _catalogStore;
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;

    private CancellationTokenSource? _signInCts;

    [ObservableProperty]
    public partial string DaemonStatus { get; private set; } = StatusUnknown;

    [ObservableProperty]
    public partial bool IsSigningIn { get; set; }
    public ObservableCollection<string> ToolsetSources { get; } = [];

    public static string StdioConfigSnippet => """
                                               {
                                                 "mcpServers": {
                                                   "revitdevtool": {
                                                     "type": "stdio",
                                                     "command": "~/AppData/Roaming/Autodesk/ApplicationPlugins/RevitDevTool.bundle/Contents/DevTools.Daemon.exe",
                                                     "args": ["--stdio"]
                                                   }
                                                 }
                                               }
                                               """;

    public McpSettingViewModel(ISettingsService settingsService, McpCatalogStore catalogStore)
    {
        _settingsService = settingsService;
        _catalogStore = catalogStore;
        _catalogStore.CatalogChanged += OnCatalogChanged;
        LoadSources();
    }

    public void Activate()
    {
        LoadSources();
        Task.Run(RefreshDaemonStatusAsync);
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task TriggerDaemonSignInAsync()
    {
        if (IsSigningIn) return;
        IsSigningIn = true;

        _signInCts = new CancellationTokenSource(TimeSpan.FromSeconds(SignInTimeoutSeconds));
        try
        {
            var ct = _signInCts.Token;
            await DaemonClient.EnsureRunningAsync(ct).ConfigureAwait(true);
            await DaemonClient.QueryAsync(IpcConstants.Methods.SignIn, ct).ConfigureAwait(true);
            await RefreshDaemonStatusAsync().ConfigureAwait(true);
        }
        finally
        {
            _signInCts.Dispose();
            _signInCts = null;
            IsSigningIn = false;
        }
    }

    [RelayCommand]
    private void CancelSignIn() => _signInCts?.Cancel();

    [RelayCommand]
    private static async Task OpenDaemonDashboardAsync()
    {
        await DaemonClient.EnsureRunningAsync().ConfigureAwait(true);
        await DaemonClient.QueryAsync(IpcConstants.Methods.OpenDashboard).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RemoveSourceAsync(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return;

        var config = _settingsService.McpRegistryConfig;
        config.DotnetPaths = config.DotnetPaths.Where(p => p != source).ToList();
        config.PythonToolsetPaths = config.PythonToolsetPaths.Where(p => p != source).ToList();

        await _catalogStore.ReloadAsync().ConfigureAwait(true);
    }

    private void LoadSources()
    {
        ToolsetSources.Clear();
        var config = _settingsService.McpRegistryConfig;
        foreach (var path in config.DotnetPaths)
            ToolsetSources.Add(path);
        foreach (var path in config.PythonToolsetPaths)
            ToolsetSources.Add(path);
    }

    private async Task RefreshDaemonStatusAsync()
    {
        var response = await DaemonClient.QueryAsync(IpcConstants.Methods.Status).ConfigureAwait(false);
        var status = IsDaemonRunning(response) ? StatusRunning : StatusNotRunning;

        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.Invoke(() => DaemonStatus = status);
            return;
        }

        DaemonStatus = status;
    }

    private static bool IsDaemonRunning(string? response)
    {
        if (string.IsNullOrWhiteSpace(response)) return false;

        try
        {
            using var doc = JsonDocument.Parse(response!);
            return doc.RootElement.TryGetProperty(IpcPropertyNames.IsRunning, out var running)
                   && running.GetBoolean();
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private void OnCatalogChanged(object? sender, EventArgs e)
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.Invoke(LoadSources);
            return;
        }

        LoadSources();
    }

    public void Dispose() => _catalogStore.CatalogChanged -= OnCatalogChanged;
}
