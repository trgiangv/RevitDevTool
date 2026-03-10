using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Text.Json;
using RevitDevTool.Utils;
using RevitDevTool.Theme;
using System.Windows.Threading;
using RevitDevTool.Contracts;
using RevitDevTool.Execution.Models;
using RevitDevTool.Mcp;
using RevitDevTool.Mcp.Models;
// ReSharper disable UnusedParameterInPartialMethod
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace RevitDevTool.ViewModel;

public sealed partial class McpRegistryViewModel : ObservableObject, IDisposable
{
    private readonly McpToolStore _toolStore;
    private readonly McpBridgeState _bridgeState;
    private readonly DispatcherTimer _searchDebounceTimer;
    private readonly Dictionary<string, int> _callCounts = new(StringComparer.OrdinalIgnoreCase);
    private int _busyDepth;
    private bool _disposed;

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private int _totalToolCount;
    [ObservableProperty] private int _dotNetToolCount;
    [ObservableProperty] private int _pythonToolCount;
    [ObservableProperty] private int _totalCalled;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _busyMessage = string.Empty;
    [ObservableProperty] private string _connectedText = "Disconnected";
    [ObservableProperty] private string _port = "N/A";
    [ObservableProperty] private McpToolItem? _selectedTool;
    [ObservableProperty] private bool _isExecuting;
    [ObservableProperty] private string _executionStatusText = "Idle";
    private ObservableCollection<McpToolItem> Tools { get; } = [];
    public ObservableCollection<McpToolItem> FilteredTools { get; } = [];
    public bool ShowStatusPanel => IsBusy || IsExecuting;
    public string StatusPanelText => IsBusy ? BusyMessage : ExecutionStatusText;

    public McpRegistryViewModel(McpToolStore toolStore, McpBridgeState bridgeState)
    {
        _toolStore = toolStore;
        _bridgeState = bridgeState;

        _searchDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _searchDebounceTimer.Tick += (_, _) =>
        {
            _searchDebounceTimer.Stop();
            ApplyFilter();
        };

        _toolStore.ToolsChanged += OnRegistryChanged;
        _bridgeState.PropertyChanged += OnBridgeStateChanged;
        _bridgeState.ToolCalls.CollectionChanged += OnToolCallsCollectionChanged;
        ThemeManager.Current.ActualApplicationThemeChanged += OnThemeChanged;

        foreach (var item in _bridgeState.ToolCalls)
        {
            item.PropertyChanged += OnToolCallMetricChanged;
            _callCounts[item.ToolId] = item.Count;
        }

        IsConnected = _bridgeState.IsConnected;
        ConnectedText = BuildConnectedText();
        Port = string.IsNullOrWhiteSpace(_bridgeState.Endpoint) ? "N/A" : _bridgeState.Endpoint;
        TotalCalled = _bridgeState.TotalToolCalls;
        RefreshExecutionState();
    }

    public async Task InitializeAsync()
    {
        using var _ = BeginBusy("Loading MCP tools...");
        await _toolStore.ReloadAsync().ConfigureAwait(true);
        RebuildToolList();
    }

    [RelayCommand]
    private async Task ReloadAsync()
    {
        using var _ = BeginBusy("Reloading MCP tools...");
        await _toolStore.ReloadAsync().ConfigureAwait(true);
    }

    public async Task AddDroppedPathAsync(string path)
    {
        using var _ = BeginBusy($"Parsing MCP toolset from '{Path.GetFileName(path)}'...");
        await _toolStore.AddPathAsync(path).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task LoadDotnetToolsetAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "DLL files (*.dll)|*.dll|All files (*.*)|*.*",
            Title = "Select .NET MCP Tool Assembly"
        };

        if (dialog.ShowDialog() == true)
        {
            using var _ = BeginBusy($"Loading MCP assembly '{Path.GetFileName(dialog.FileName)}'...");
            await _toolStore.AddPathAsync(dialog.FileName).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task LoadPythonToolsetAsync()
    {
        var selectedFolder = SettingsUtils.SelectFolder("Select Python MCP Toolset Folder");
        if (!string.IsNullOrWhiteSpace(selectedFolder))
        {
            using var _ = BeginBusy($"Parsing MCP toolset from '{Path.GetFileName(selectedFolder)}'...");
            await _toolStore.AddPathAsync(selectedFolder).ConfigureAwait(true);
        }
    }

    private void OnRegistryChanged(object? sender, EventArgs e)
    {
        if (!_searchDebounceTimer.Dispatcher.CheckAccess())
        {
            _searchDebounceTimer.Dispatcher.Invoke(RebuildToolList);
            return;
        }

        RebuildToolList();
    }

    private void RebuildToolList()
    {
        Tools.Clear();
        foreach (var definition in _toolStore.Tools)
        {
            _callCounts.TryGetValue(definition.ToolId, out var count);
            var toolItem = new McpToolItem
            {
                ToolId = definition.ToolId,
                Name = definition.Name,
                DisplayName = definition.DisplayName,
                SourceAddress = definition.SourceAddress,
                GroupName = definition.GroupName,
                Description = definition.Description,
                ToolTipText = BuildToolTipText(definition),
                SourceKind = definition.SourceKind,
                CallCount = count,
                InputSchemaJson = definition.InputSchemaJson,
            };
            Tools.Add(toolItem);
        }

        TotalToolCount = Tools.Count;
        DotNetToolCount = Tools.Count(item => item.SourceKind == ExecutionMode.Assembly);
        PythonToolCount = Tools.Count(item => item.SourceKind == ExecutionMode.Python);
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        FilteredTools.Clear();
        var query = SearchText.Trim();
        var hasQuery = !string.IsNullOrWhiteSpace(query);
        var queryLower = query.ToLowerInvariant();

        IEnumerable<McpToolItem> source = Tools;
        if (hasQuery)
        {
            source = source.Where(item =>
                item.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.GroupName.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        source = source
            .OrderBy(item => item.GroupName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase);

        var isDark = ThemeManager.Current.ActualApplicationTheme == AppTheme.Dark;
        foreach (var item in source)
        {
            item.NameHighlight = BuildHighlight(item.Name, queryLower, hasQuery, isDark);
            item.GroupNameHighlight = BuildHighlight(item.GroupName, queryLower, hasQuery, isDark);
            FilteredTools.Add(item);
        }
    }

    private static HighlightRange? BuildHighlight(string text, string queryLower, bool hasQuery, bool isDark)
    {
        if (!hasQuery || string.IsNullOrEmpty(text))
            return null;
        var idx = text.ToLowerInvariant().IndexOf(queryLower, StringComparison.Ordinal);
        return idx < 0 ? null : new HighlightRange(idx, idx + queryLower.Length) { DarkSkin = isDark };
    }

    partial void OnSearchTextChanged(string value)
    {
        _searchDebounceTimer.Stop();
        _searchDebounceTimer.Start();
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(SearchText))
            ApplyFilter();
    }

    partial void OnIsBusyChanged(bool value)
    {
        RaiseStatusComputedProperties();
    }

    partial void OnBusyMessageChanged(string value)
    {
        RaiseStatusComputedProperties();
    }

    partial void OnIsExecutingChanged(bool value)
    {
        RaiseStatusComputedProperties();
    }

    partial void OnExecutionStatusTextChanged(string value)
    {
        RaiseStatusComputedProperties();
    }

    private void OnBridgeStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_searchDebounceTimer.Dispatcher.CheckAccess())
        {
            _searchDebounceTimer.Dispatcher.Invoke(() => OnBridgeStateChanged(sender, e));
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(McpBridgeState.IsConnected) or nameof(McpBridgeState.Endpoint):
                IsConnected = _bridgeState.IsConnected;
                ConnectedText = BuildConnectedText();
                Port = string.IsNullOrWhiteSpace(_bridgeState.Endpoint) ? "N/A" : _bridgeState.Endpoint;
                break;
            case nameof(McpBridgeState.TotalToolCalls):
                TotalCalled = _bridgeState.TotalToolCalls;
                break;
            case nameof(McpBridgeState.QueueDepth):
            case nameof(McpBridgeState.IsExecuting):
            case nameof(McpBridgeState.CurrentToolName):
            case nameof(McpBridgeState.CurrentStage):
            case nameof(McpBridgeState.CurrentStatusMessage):
                RefreshExecutionState();
                break;
        }
    }

    private void OnToolCallsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems.OfType<McpToolCallMetric>())
                item.PropertyChanged += OnToolCallMetricChanged;
        }

        if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems.OfType<McpToolCallMetric>())
                item.PropertyChanged -= OnToolCallMetricChanged;
        }

        RebuildCallCountCache();
        foreach (var tool in Tools)
        {
            _callCounts.TryGetValue(tool.ToolId, out var count);
            tool.CallCount = count;
        }

    }

    private void OnToolCallMetricChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(McpToolCallMetric.Count))
            return;

        RebuildCallCountCache();
        foreach (var tool in Tools)
        {
            _callCounts.TryGetValue(tool.ToolId, out var count);
            tool.CallCount = count;
        }
    }


    private void RebuildCallCountCache()
    {
        _callCounts.Clear();
        foreach (var call in _bridgeState.ToolCalls)
            _callCounts[call.ToolId] = call.Count;
    }

    private void RefreshExecutionState()
    {
        IsExecuting = _bridgeState.IsExecuting;
        ExecutionStatusText = _bridgeState.IsExecuting
            ? string.IsNullOrWhiteSpace(_bridgeState.CurrentStatusMessage) ? "Running MCP tool..." : _bridgeState.CurrentStatusMessage
            : "Idle";
    }

    private void RaiseStatusComputedProperties()
    {
        OnPropertyChanged(nameof(ShowStatusPanel));
        OnPropertyChanged(nameof(StatusPanelText));
    }

    private string BuildConnectedText()
    {
        return _bridgeState.IsConnected ? "Connected" : "Disconnected";
    }

    private static string BuildToolTipText(McpToolDefinition definition)
    {
        var builder = new StringBuilder();
        builder.AppendLine(definition.Name);
        if (!string.IsNullOrWhiteSpace(definition.SourceAddress))
        {
            builder.AppendLine(definition.SourceAddress);
        }
        builder.AppendLine();
        builder.AppendLine(string.IsNullOrWhiteSpace(definition.Description) ? "No description." : definition.Description.Trim());

        var arguments = BuildArgumentSummary(definition.InputSchemaJson);
        if (!string.IsNullOrWhiteSpace(arguments))
        {
            builder.AppendLine();
            builder.AppendLine("Args:");
            builder.Append(arguments);
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildArgumentSummary(string? inputSchemaJson)
    {
        if (string.IsNullOrWhiteSpace(inputSchemaJson))
            return string.Empty;

        try
        {
            var schema = JsonSerializer.Deserialize<InputSchema>(inputSchemaJson!);
            if (schema?.Properties is not { Count: > 0 })
                return string.Empty;

            var lines = schema.Properties.Select(kvp =>
            {
                var prop = kvp.Value;
                var type = prop.Type ?? "any";
                var title = prop.Title ?? kvp.Key;
                var desc = string.IsNullOrWhiteSpace(prop.Description) ? string.Empty : $" — {prop.Description}";
                return $"- {kvp.Key}: {title} ({type}){desc}";
            });

            return string.Join(Environment.NewLine, lines);
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private BusyScope BeginBusy(string message)
    {
        _busyDepth++;
        IsBusy = true;
        BusyMessage = message;
        return new BusyScope(this);
    }

    private void EndBusy()
    {
        _busyDepth = Math.Max(0, _busyDepth - 1);
        if (_busyDepth != 0)
            return;

        IsBusy = false;
        BusyMessage = string.Empty;
    }

    private sealed class BusyScope(McpRegistryViewModel owner) : IDisposable
    {
        private McpRegistryViewModel? _owner = owner;

        public void Dispose()
        {
            _owner?.EndBusy();
            _owner = null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _toolStore.ToolsChanged -= OnRegistryChanged;
        _bridgeState.PropertyChanged -= OnBridgeStateChanged;
        _bridgeState.ToolCalls.CollectionChanged -= OnToolCallsCollectionChanged;
        ThemeManager.Current.ActualApplicationThemeChanged -= OnThemeChanged;
        foreach (var item in _bridgeState.ToolCalls)
            item.PropertyChanged -= OnToolCallMetricChanged;
    }
}