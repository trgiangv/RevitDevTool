using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Autodesk.Revit.DB;
using RevitDevTool.Scintilla.Control;
using RevitDevTool.Scintilla.Core;
using RevitDevTool.Scintilla.Render;
using RevitDevTool.Scintilla.Search;
using ZLogger;

namespace RevitDevTool.Scintilla.Wpf.Demo;

public partial class MainWindow : Window
{
    private readonly ScintillaLogViewerWpf _viewer;
    private readonly ILogViewerController _controller;
    private readonly ILogViewerControlEvents _controlEvents;
    private readonly ILogger<MainWindow> _logger;
    private readonly DemoEnrichmentCallbacks _enrichmentCallbacks;
    private readonly DispatcherTimer _metricsTimer;

    private CancellationTokenSource? _floodCts;
    private Task? _floodTask;
    private long _incomingCount;
    private long _lastIncomingCount;
    private long _lastRenderedCount;

    public MainWindow(
        ScintillaLogViewerWpf viewer,
        ILogViewerControlEvents controlEvents,
        ILogger<MainWindow> logger,
        DemoEnrichmentCallbacks enrichmentCallbacks)
    {
        InitializeComponent();

        _viewer = viewer;
        _controller = _viewer.Controller;
        _controlEvents = controlEvents;
        _logger = logger;
        _enrichmentCallbacks = enrichmentCallbacks;

        ViewerContainer.Content = _viewer.HostElement;
        InitializeControls();
        HookEvents();
        SeedInitialLogs();

        _metricsTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _metricsTimer.Tick += (_, _) => RefreshMetrics();
        _metricsTimer.Start();
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        _controlEvents.RequestStart();
    }

    protected override void OnClosed(EventArgs e)
    {
        _metricsTimer.Stop();
        _controlEvents.RenderModeChanged -= OnRenderModeChanged;
        _controlEvents.ThemeChanged -= OnThemeChanged;
        StopFloodAsync().GetAwaiter().GetResult();
        _controlEvents.RequestStop();
        _viewer.Dispose();
        base.OnClosed(e);
    }

    private void InitializeControls()
    {
        ClearModeCombo.ItemsSource = new[] { nameof(ClearMode.Fast), nameof(ClearMode.Aggressive) };
        ClearModeCombo.SelectedIndex = 0;

        LevelFilterCombo.ItemsSource = new[]
        {
            "All",
            nameof(LogLevel.Trace),
            nameof(LogLevel.Debug),
            nameof(LogLevel.Information),
            nameof(LogLevel.Warning),
            nameof(LogLevel.Error),
            nameof(LogLevel.Critical)
        };
        LevelFilterCombo.SelectedIndex = 0;
        PrettyJsonCheckBox.IsChecked = true;
        ThemeCombo.ItemsSource = new[] { "Dark", "Light" };
        ThemeCombo.SelectedItem = "Dark";
    }

    private void HookEvents()
    {
        _controlEvents.RenderModeChanged += OnRenderModeChanged;
        _controlEvents.ThemeChanged += OnThemeChanged;

        StartFloodButton.Click += (_, _) => StartFlood();
        StopFloodButton.Click += async (_, _) => await StopFloodAsync();
        ClearButton.Click += (_, _) =>
        {
            _controlEvents.RequestClear(ParseClearMode(ClearModeCombo.SelectedItem?.ToString()));
            Interlocked.Exchange(ref _incomingCount, 0);
        };
        FindNextButton.Click += (_, _) => FindNext();
        SearchTextBox.TextChanged += (_, _) => ApplyFilter();
        SearchTextBox.KeyDown += (_, args) =>
        {
            if (args.Key == System.Windows.Input.Key.Enter)
                FindNext();
        };
        LevelFilterCombo.SelectionChanged += (_, _) => ApplyFilter();
        PrettyJsonCheckBox.Checked += (_, _) =>
        {
            _controlEvents.RequestRenderMode(true);
        };
        PrettyJsonCheckBox.Unchecked += (_, _) =>
        {
            _controlEvents.RequestRenderMode(false);
        };
        TokenCallbackCheckBox.Checked += (_, _) => _enrichmentCallbacks.EnableTokenResolution = true;
        TokenCallbackCheckBox.Unchecked += (_, _) => _enrichmentCallbacks.EnableTokenResolution = false;
        ThemeCombo.SelectionChanged += (_, _) =>
        {
            var isLight = string.Equals(ThemeCombo.SelectedItem?.ToString(), "Light", StringComparison.OrdinalIgnoreCase);
            var theme = isLight ? ScintillaTheme.EnhancedLight : ScintillaTheme.EnhancedDark;
            _controlEvents.RequestTheme(theme);
        };
    }

    private void SeedInitialLogs()
    {
        for (var i = 0; i < 100; i++)
        {
            var elementId = new ElementId(i + 10_000);
            using var _ = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["Session"] = "WpfWarmup",
                ["ElementId"] = elementId
            });
            _logger.ZLogInformation(
                $"Aurecon WPF warmup index={i}, elementId={elementId}, uniqueId={"8d2f3f31-2b9f-4f6e-a18d-3e7a8f3b1c2d-0001a2b3"}, ifcGuid={"2Zw$n7f8AJvQk4r7sP9mA$"}, url={"https://example.com/revit/wpf"}");
        }
        const string warmupJson = "{\"event\":\"wpf-warmup\",\"state\":\"ok\",\"count\":100}";
        _logger.ZLogInformation($"{warmupJson}");
        _logger.ZLogInformation($"___ WPF URL DETECT TESTS ___");
        _logger.ZLogInformation(
            $"Wpf url={"https://example.com/revit/wpf?tab=inspect&mode=token#quick"} trailing={"https://example.com/revit/wpf/docs"},");
        _logger.ZLogInformation(
            $"Wpf mixed: www={"www.contoso.com/revit/wpf/help"} mail={"mailto:wpf-support@example.com"}");
    }

    private void StartFlood()
    {
        if (_floodCts is not null)
            return;

        _floodCts = new CancellationTokenSource();
        var token = _floodCts.Token;
        StartFloodButton.IsEnabled = false;
        StopFloodButton.IsEnabled = true;

        _floodTask = Task.Run(async () =>
        {
            var i = 0;
            while (!token.IsCancellationRequested)
            {
                var id = i++;
                Interlocked.Increment(ref _incomingCount);

                using var _ = _logger.BeginScope(new Dictionary<string, object?>
                {
                    ["Flow"] = "WpfFlood",
                    ["ElementId"] = id % 200_000
                });
                _logger.ZLogInformation(
                    $"wpf flood index={id}, traceId={Guid.NewGuid()}, uniqueId={"8d2f3f31-2b9f-4f6e-a18d-3e7a8f3b1c2d-0001a2b3"}, ifcGuid={"2Zw$n7f8AJvQk4r7sP9mA$"}, url={"https://example.com/revit/wpf"}");

                if (id % 10 == 0)
                {
                    _logger.ZLogWarning($"WPF warning at index={id}");
                }
                if (id % 41 == 0)
                {
                    _logger.ZLogError($"WPF failure at index={id}, error={"WPF simulated exception"}");
                }

                if (id % 1000 == 0)
                    await Task.Delay(5, token).ConfigureAwait(false);
            }
        }, token);
    }

    private async Task StopFloodAsync()
    {
        if (_floodCts is null)
            return;

        _floodCts.Cancel();
        if (_floodTask is not null)
        {
            try
            {
                await _floodTask.ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                // Expected while stopping.
            }
        }

        _floodTask = null;
        _floodCts.Dispose();
        _floodCts = null;
        StartFloodButton.IsEnabled = true;
        StopFloodButton.IsEnabled = false;
    }

    private void ApplyFilter()
    {
        var options = new LogFilterOptions
        {
            TextContains = string.IsNullOrWhiteSpace(SearchTextBox.Text) ? null : SearchTextBox.Text,
            MatchCase = false,
            AllowedLevels = ParseLevelFilter(LevelFilterCombo.SelectedItem?.ToString())
        };

        _controlEvents.RequestFilter(options);
    }

    private void FindNext()
    {
        var pattern = SearchTextBox.Text;
        if (string.IsNullOrWhiteSpace(pattern))
            return;

        _controlEvents.RequestSearch(new LogSearchRequest
        {
            Pattern = pattern,
            MatchCase = false,
            UseRegex = false
        });
    }

    private static HashSet<LogLevel> ParseLevelFilter(string? selected)
    {
        if (string.IsNullOrWhiteSpace(selected) || selected == "All")
            return new HashSet<LogLevel>();

        return Enum.TryParse<LogLevel>(selected, out var parsed)
            ? new HashSet<LogLevel> { parsed }
            : new HashSet<LogLevel>();
    }

    private static ClearMode ParseClearMode(string? selected)
        => Enum.TryParse<ClearMode>(selected, out var parsed) ? parsed : ClearMode.Fast;

    private void OnRenderModeChanged(bool enablePrettyJson)
    {
        _enrichmentCallbacks.EnablePrettyJson = enablePrettyJson;
    }

    private void OnThemeChanged(ScintillaTheme theme)
    {
        _viewer.TrySetTheme(theme);
    }

    private void RefreshMetrics()
    {
        var incoming = Interlocked.Read(ref _incomingCount);
        var rendered = _controller.RenderedMessages;
        var dropped = _controller.DroppedMessages;
        var attempted = _controller.AttemptedWrites;
        var accepted = _controller.AcceptedWrites;
        var localFail = _controller.LocalWriteFails;
        var dropEstimate = _controller.DroppedByPolicyEstimate;
        var backlogEstimate = _controller.IngestBacklogEstimate;
        var lineCount = _viewer.ScintillaControl.Lines.Count;

        var incomingRate = incoming - Interlocked.Exchange(ref _lastIncomingCount, incoming);
        var renderedRate = rendered - Interlocked.Exchange(ref _lastRenderedCount, rendered);

        var process = Process.GetCurrentProcess();
        var workingSetMb = process.WorkingSet64 / 1024d / 1024d;
        var privateMb = process.PrivateMemorySize64 / 1024d / 1024d;
        var managedMb = GC.GetTotalMemory(false) / 1024d / 1024d;

        MetricsTextBlock.Text =
            $"incoming/s: {incomingRate:n0} | rendered/s: {renderedRate:n0} | dropped(local): {dropped:n0} | " +
            $"attempted: {attempted:n0} | accepted: {accepted:n0} | localFail: {localFail:n0} | " +
            $"dropEst: {dropEstimate:n0} | backlog(est): {backlogEstimate:n0} | " +
            $"history: {_controller.HistoryEntries:n0} | lines: {lineCount:n0} | " +
            $"ws: {workingSetMb:n1} MB | private: {privateMb:n1} MB | managed: {managedMb:n1} MB";
    }
}
