using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows.Threading;
using DevTools.Execution.External.Connections;
using DevTools.Execution.External.Mcp;
using DevTools.McpParser.Models;
using DevTools.Presentation.Models;
using DevTools.UI.Behaviors;
using DevTools.UI.Theme;
using DevTools.Utilities;
// ReSharper disable UnusedParameterInPartialMethod
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace DevTools.Presentation.ViewModels;

public sealed partial class McpRegistryViewModel : ObservableObject, IBusyViewModel, IDisposable
{
    private readonly ToolRegistryStore _toolStore;
    private readonly ConnectionState _bridgeState;
    private readonly DispatcherTimer _searchDebounceTimer;
    private readonly DispatcherTimer _elapsedTimer;
    private readonly Dictionary<string, int> _callCounts = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsConnected { get; set; }

    [ObservableProperty]
    public partial int TotalToolCount { get; set; }

    [ObservableProperty]
    public partial int DotNetToolCount { get; set; }

    [ObservableProperty]
    public partial int PythonToolCount { get; set; }

    [ObservableProperty]
    public partial int TotalCalled { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string BusyMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PipeName { get; set; } = "N/A";

    [ObservableProperty]
    public partial McpToolItem? SelectedTool { get; set; }

    [ObservableProperty]
    public partial bool IsExecuting { get; set; }

    [ObservableProperty]
    public partial string ExecutionStatusText { get; set; } = "Idle";
    private ObservableCollection<McpToolItem> Tools { get; } = [];
    public ObservableCollection<McpToolItem> FilteredTools { get; } = [];
    public bool ShowStatusPanel => IsBusy || IsExecuting;
    public string StatusPanelText => IsBusy ? BusyMessage : ExecutionStatusText;

    public McpRegistryViewModel(ToolRegistryStore toolStore, ConnectionState bridgeState)
    {
        _toolStore = toolStore;
        _bridgeState = bridgeState;

        _searchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _searchDebounceTimer.Tick += (_, _) =>
        {
            _searchDebounceTimer.Stop();
            ApplyFilter();
        };

        _elapsedTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _elapsedTimer.Tick += (_, _) => UpdateElapsedDisplay();

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
        PipeName = string.IsNullOrWhiteSpace(_bridgeState.Endpoint) ? "N/A" : _bridgeState.Endpoint;
        TotalCalled = _bridgeState.TotalToolCalls;
        RefreshExecutionState();
    }

    public async Task InitializeAsync()
    {
        await this.WhileBusy("Loading MCP tools...", async () =>
        {
            await _toolStore.ReloadAsync().ConfigureAwait(true);
            RebuildToolList();
        });
    }

    [RelayCommand]
    private void Reload()
    {
        this.WhileBusy("Reloading MCP tools...", () =>
        {
            HostUiHelper.RunOnMainThread(async void () =>
            {
                try { await _toolStore.ReloadAsync(); }
                catch { /* ignore */ }
            });
        });
    }

    public async Task AddDroppedPathAsync(string path)
    {
        await this.WhileBusy($"Parsing MCP toolset from '{Path.GetFileName(path)}'...", async () =>
        {
            await _toolStore.AddPathAsync(path).ConfigureAwait(true);
        });
    }

    [RelayCommand]
    private async Task LoadDotnetToolsetAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "DLL files (*.dll)|*.dll|All files (*.*)|*.*",
            Title = "Select .NET MCP Tool Assembly"
        };
        if (dialog.ShowDialog() != true) return;
        await this.WhileBusy($"Loading MCP assembly '{Path.GetFileName(dialog.FileName)}'...", async () =>
        {
            await _toolStore.AddPathAsync(dialog.FileName).ConfigureAwait(true);
        });
    }

    [RelayCommand]
    private async Task LoadPythonToolsetAsync()
    {
        var selectedFolder = AppUtils.SelectFolder("Select Python MCP Toolset Folder");
        if (string.IsNullOrWhiteSpace(selectedFolder)) return;
        await this.WhileBusy($"Parsing MCP toolset from '{Path.GetFileName(selectedFolder)}'...", async () =>
        {
            await _toolStore.AddPathAsync(selectedFolder).ConfigureAwait(true);
        });
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
        foreach (var tool in _toolStore.ToolCatalog)
        {
            var protocolTool = tool.ProtocolTool;
            var binding = tool.Binding;
            _callCounts.TryGetValue(tool.Id, out var count);
            Tools.Add(new McpToolItem
            {
                ToolId = tool.Id,
                Name = protocolTool.Name,
                DisplayName = protocolTool.Title ?? protocolTool.Name,
                SourceAddress = binding.SourceAddress,
                GroupName = binding.GroupName,
                Description = protocolTool.Description ?? string.Empty,
                ToolTipText = BuildToolTipText(tool),
                SourceKind = binding.SourceKind,
                CallCount = count,
                InputSchemaJson = protocolTool.InputSchema.GetRawText(),
            });
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
        if (!hasQuery || string.IsNullOrEmpty(text)) return null;
        var idx = text.ToLowerInvariant().IndexOf(queryLower, StringComparison.Ordinal);
        return idx < 0 ? null : new HighlightRange(idx, idx + queryLower.Length) { DarkSkin = isDark };
    }

    partial void OnSearchTextChanged(string value) { _searchDebounceTimer.Stop(); _searchDebounceTimer.Start(); }
    private void OnThemeChanged(object? sender, EventArgs e) { if (!string.IsNullOrWhiteSpace(SearchText)) ApplyFilter(); }
    partial void OnIsBusyChanged(bool value) => RaiseStatusComputedProperties();
    partial void OnBusyMessageChanged(string value) => RaiseStatusComputedProperties();
    partial void OnIsExecutingChanged(bool value) => RaiseStatusComputedProperties();
    partial void OnExecutionStatusTextChanged(string value) => RaiseStatusComputedProperties();

    private void OnBridgeStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_searchDebounceTimer.Dispatcher.CheckAccess())
        {
            _searchDebounceTimer.Dispatcher.Invoke(() => OnBridgeStateChanged(sender, e));
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(ConnectionState.IsConnected) or nameof(ConnectionState.Endpoint):
                IsConnected = _bridgeState.IsConnected;
                PipeName = string.IsNullOrWhiteSpace(_bridgeState.Endpoint) ? "N/A" : _bridgeState.Endpoint;
                break;
            case nameof(ConnectionState.TotalToolCalls):
                TotalCalled = _bridgeState.TotalToolCalls;
                break;
            case nameof(ConnectionState.IsExecuting):
                RefreshExecutionState();
                break;
            case nameof(ConnectionState.CurrentToolName):
            case nameof(ConnectionState.CurrentStatusMessage):
                UpdateElapsedDisplay();
                break;
        }
    }

    private void OnToolCallsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
            foreach (var item in e.NewItems.OfType<ToolCallMetric>())
                item.PropertyChanged += OnToolCallMetricChanged;
        if (e.OldItems is not null)
            foreach (var item in e.OldItems.OfType<ToolCallMetric>())
                item.PropertyChanged -= OnToolCallMetricChanged;
        SyncCallCounts();
    }

    private void OnToolCallMetricChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ToolCallMetric.Count)) SyncCallCounts();
    }

    private void SyncCallCounts()
    {
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
        if (_bridgeState.IsExecuting)
        {
            UpdateElapsedDisplay();
            _elapsedTimer.Start();
        }
        else
        {
            _elapsedTimer.Stop();
            ExecutionStatusText = "Idle";
        }
    }

    private void UpdateElapsedDisplay()
    {
        if (!_bridgeState.IsExecuting) return;
        var started = _bridgeState.ExecutionStartedAtUtc;
        var elapsed = started == default ? TimeSpan.Zero : DateTime.UtcNow - started;
        var toolName = _bridgeState.CurrentToolName;
        var status = _bridgeState.CurrentStatusMessage;
        var timeText = elapsed.TotalSeconds < 1 ? string.Empty
            : elapsed.TotalMinutes >= 1 ? $" ({elapsed.Minutes}m {elapsed.Seconds}s)" : $" ({elapsed.TotalSeconds:F0}s)";
        ExecutionStatusText = string.IsNullOrWhiteSpace(status)
            ? $"Running '{toolName}'...{timeText}" : $"{status}{timeText}";
    }

    private void RaiseStatusComputedProperties()
    {
        OnPropertyChanged(nameof(ShowStatusPanel));
        OnPropertyChanged(nameof(StatusPanelText));
    }

    private static string BuildToolTipText(McpRegisteredTool tool)
    {
        var protocolTool = tool.ProtocolTool;
        var binding = tool.Binding;
        var builder = new StringBuilder();
        builder.AppendLine(protocolTool.Name);
        if (!string.IsNullOrWhiteSpace(binding.SourceAddress))
            builder.AppendLine(binding.SourceAddress);
        builder.AppendLine();
        builder.AppendLine(string.IsNullOrWhiteSpace(protocolTool.Description) ? "No description." : protocolTool.Description!.Trim());

        var arguments = BuildArgumentSummary(protocolTool.InputSchema.GetRawText());
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
        if (string.IsNullOrWhiteSpace(inputSchemaJson)) return string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(inputSchemaJson!);
            if (!doc.RootElement.TryGetProperty("properties", out var properties) || properties.ValueKind != JsonValueKind.Object)
                return string.Empty;

            var lines = new List<string>();
            foreach (var prop in properties.EnumerateObject())
            {
                var type = prop.Value.TryGetProperty("type", out var typeEl) ? typeEl.GetString() ?? "any" : "any";
                var title = prop.Value.TryGetProperty("title", out var titleEl) ? titleEl.GetString() ?? prop.Name : prop.Name;
                var desc = prop.Value.TryGetProperty("description", out var descEl) ? descEl.GetString() : null;
                var descSuffix = string.IsNullOrWhiteSpace(desc) ? string.Empty : $" — {desc}";
                lines.Add($"- {prop.Name}: {title} ({type}){descSuffix}");
            }
            return string.Join(Environment.NewLine, lines);
        }
        catch (JsonException) { return string.Empty; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _elapsedTimer.Stop();
        _toolStore.ToolsChanged -= OnRegistryChanged;
        _bridgeState.PropertyChanged -= OnBridgeStateChanged;
        _bridgeState.ToolCalls.CollectionChanged -= OnToolCallsCollectionChanged;
        ThemeManager.Current.ActualApplicationThemeChanged -= OnThemeChanged;
        foreach (var item in _bridgeState.ToolCalls)
            item.PropertyChanged -= OnToolCallMetricChanged;
    }
}
