using DevTools.Presentation.Services;
using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace DevTools.Presentation.ViewModels.Settings;

public sealed partial class McpSettingViewModel : ObservableObject, IDisposable
{
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

    public McpSettingViewModel(McpCatalogStore catalogStore)
    {
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
        await _catalogStore.RemovePathAsync(source).ConfigureAwait(true);
    }

    private void LoadSources()
    {
        ToolsetSources.Clear();
        foreach (var path in _catalogStore.ToolsetPaths)
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
