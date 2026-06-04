using System.Collections.ObjectModel;
using DevTools.Execution.External.Mcp;
using DevTools.Settings;

namespace DevTools.Presentation.ViewModels;

public sealed partial class McpConfigViewModel(ISettingsService settingsService, ToolRegistryStore toolStore) : ObservableObject
{
    private List<string> _originalToolsetSources = [];

    [ObservableProperty] private bool _hasToolsetChanges;

    public ObservableCollection<string> ToolsetSources { get; } = [];

    public static string McpConfigSnippet => """
                                             {
                                               "mcpServers": {
                                                 "revitdevtool": {
                                                   "type": "stdio",
                                                   "command": "~/AppData/Roaming/Autodesk/ApplicationPlugins/RevitDevTool.bundle/Contents/MCPServer.exe",
                                                   "args": []
                                                 }
                                               }
                                             }
                                             """;

    public event EventHandler? Applied;

    public void Load()
    {
        ToolsetSources.Clear();
        _originalToolsetSources = [];

        foreach (var path in settingsService.McpRegistryConfig.DotnetPaths)
        {
            ToolsetSources.Add(path);
            _originalToolsetSources.Add(path);
        }
        foreach (var path in settingsService.McpRegistryConfig.PythonToolsetPaths)
        {
            ToolsetSources.Add(path);
            _originalToolsetSources.Add(path);
        }

        HasToolsetChanges = false;
    }

    [RelayCommand]
    private void RemoveToolsetSource(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return;
        ToolsetSources.Remove(source);
        RefreshToolsetChanges();
    }

    [RelayCommand(CanExecute = nameof(HasToolsetChanges))]
    private async Task ApplyAsync()
    {
        var config = settingsService.McpRegistryConfig;
        var originalDotnet = config.DotnetPaths.ToList();
        var originalPython = config.PythonToolsetPaths.ToList();

        var dotnetPaths = new List<string>();
        var pythonPaths = new List<string>();

        foreach (var source in ToolsetSources)
        {
            if (originalDotnet.Contains(source))
                dotnetPaths.Add(source);
            else if (originalPython.Contains(source))
                pythonPaths.Add(source);
        }

        config.DotnetPaths = dotnetPaths;
        config.PythonToolsetPaths = pythonPaths;

        await toolStore.ReloadAsync().ConfigureAwait(true);
        Applied?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshToolsetChanges()
    {
        var current = ToolsetSources.ToList();
        HasToolsetChanges = current.Count != _originalToolsetSources.Count ||
                            !current.SequenceEqual(_originalToolsetSources);
        ApplyCommand.NotifyCanExecuteChanged();
    }
}
