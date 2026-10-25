using DevTools.Mcp;
using DevTools.Presentation.Services;
using DevTools.Settings;
using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace DevTools.Presentation.ViewModels.Settings;

public sealed partial class McpSettingViewModel : ObservableObject, IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly McpCatalogStore _catalogStore;
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;

    public ObservableCollection<string> ToolsetSources { get; } = [];

    public string StdioConfigSnippet { get; } = $$"""
                                                  {
                                                    "mcpServers": {
                                                      "revitdevtool": {
                                                        "type": "stdio",
                                                        "command": "{{Utilities.AppUtils.GetDaemonExePath().Replace('\\', '/')}}",
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

    public void Activate() => LoadSources();

    [RelayCommand]
    private async Task TriggerDaemonSignInAsync()
    {
        await DaemonClient.EnsureRunningAsync().ConfigureAwait(true);
        await DaemonClient.SendAsync(IpcConstants.Methods.SignIn).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RemoveSourceAsync(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return;

        var config = _settingsService.McpRegistryConfig;
        config.DotnetPaths = config.DotnetPaths.Where(p => p != source).ToList();
        config.PythonPaths = config.PythonPaths.Where(p => p != source).ToList();

        await _catalogStore.ReloadAsync().ConfigureAwait(true);
    }

    private void LoadSources()
    {
        ToolsetSources.Clear();
        var config = _settingsService.McpRegistryConfig;
        foreach (var path in config.DotnetPaths)
            ToolsetSources.Add(path);
        foreach (var path in config.PythonPaths)
            ToolsetSources.Add(path);
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
