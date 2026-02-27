using System.Diagnostics;
using Microsoft.Extensions.Logging;
using RevitDevTool.Scintilla;
using RevitDevTool.Scintilla.Contracts;
using RevitDevTool.Scintilla.Search;
using ZLogger;

namespace RevitDevTool.Scintilla.Demo;

internal sealed class MainForm : Form
{
    private readonly ScintillaLogViewerHost _viewerHost;
    private readonly ILogViewerController _controller;
    private readonly ILogger _logger;
    private readonly System.Windows.Forms.Timer _metricsTimer;

    private readonly Button _startFloodButton = new() { Text = "Start Flood", Width = 110 };
    private readonly Button _stopFloodButton = new() { Text = "Stop Flood", Width = 110, Enabled = false };
    private readonly Button _clearButton = new() { Text = "Clear", Width = 80 };
    private readonly ComboBox _clearModeCombo = new() { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _searchTextBox = new() { Width = 240, PlaceholderText = "Search..." };
    private readonly Button _findNextButton = new() { Text = "Find Next", Width = 100 };
    private readonly ComboBox _levelFilterCombo = new() { Width = 130, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Label _metricsLabel = new() { AutoSize = true };

    private CancellationTokenSource? _floodCts;
    private Task? _floodTask;
    private long _incomingCount;
    private long _lastIncomingCount;
    private long _lastRenderedCount;

    public MainForm(ScintillaLogViewerHost viewerHost, ILogger<MainForm> logger)
    {
        Text = "RevitDevTool.Scintilla Demo (ZLogger)";
        Width = 1450;
        Height = 900;

        _viewerHost = viewerHost;
        _controller = _viewerHost.Controller;
        _logger = logger;

        BuildLayout();
        HookEvents();
        SeedInitialLogs();

        _metricsTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _metricsTimer.Tick += (_, _) => RefreshMetrics();
        _metricsTimer.Start();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _metricsTimer.Stop();
        StopFloodAsync().GetAwaiter().GetResult();
        _viewerHost.Dispose();
        base.OnFormClosing(e);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _controller.Start();
    }

    private void BuildLayout()
    {
        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            Padding = new Padding(8),
            FlowDirection = FlowDirection.LeftToRight
        };

        _levelFilterCombo.Items.AddRange(new object[]
        {
            "All",
            nameof(LogSeverity.Trace),
            nameof(LogSeverity.Debug),
            nameof(LogSeverity.Information),
            nameof(LogSeverity.Warning),
            nameof(LogSeverity.Error),
            nameof(LogSeverity.Critical)
        });
        _levelFilterCombo.SelectedIndex = 0;
        _clearModeCombo.Items.AddRange(new object[]
        {
            nameof(ClearMode.Fast),
            nameof(ClearMode.Aggressive)
        });
        _clearModeCombo.SelectedIndex = 0;

        toolbar.Controls.Add(_startFloodButton);
        toolbar.Controls.Add(_stopFloodButton);
        toolbar.Controls.Add(_clearButton);
        toolbar.Controls.Add(new Label { Text = "Clear:", AutoSize = true, Margin = new Padding(8, 8, 0, 0) });
        toolbar.Controls.Add(_clearModeCombo);
        toolbar.Controls.Add(new Label { Text = "Level:", AutoSize = true, Margin = new Padding(12, 8, 0, 0) });
        toolbar.Controls.Add(_levelFilterCombo);
        toolbar.Controls.Add(new Label { Text = "Search:", AutoSize = true, Margin = new Padding(12, 8, 0, 0) });
        toolbar.Controls.Add(_searchTextBox);
        toolbar.Controls.Add(_findNextButton);
        toolbar.Controls.Add(new Label { Text = "  ", AutoSize = true });
        toolbar.Controls.Add(_metricsLabel);

        Controls.Add(_viewerHost.HostControl);
        Controls.Add(toolbar);
    }

    private void HookEvents()
    {
        _startFloodButton.Click += (_, _) => StartFlood();
        _stopFloodButton.Click += async (_, _) => await StopFloodAsync();
        _clearButton.Click += (_, _) =>
        {
            _controller.Clear(ParseClearMode(_clearModeCombo.SelectedItem?.ToString()));
            Interlocked.Exchange(ref _incomingCount, 0);
        };
        _findNextButton.Click += (_, _) => FindNext();
        _searchTextBox.KeyDown += (_, args) =>
        {
            if (args.KeyCode == Keys.Enter)
            {
                FindNext();
                args.Handled = true;
                args.SuppressKeyPress = true;
            }
        };
        _levelFilterCombo.SelectedIndexChanged += (_, _) => ApplyFilter();
        _searchTextBox.TextChanged += (_, _) => ApplyFilter();
    }

    private void SeedInitialLogs()
    {
        for (var i = 0; i < 100; i++)
        {
            using (_logger.BeginScope(new Dictionary<string, object?>
                   {
                       ["Session"] = "Warmup",
                       ["WarmupIndex"] = i
                   }))
            {
                _logger.LogInformation("Initial warmup log line {Index}", i);
            }
        }
    }

    private void StartFlood()
    {
        if (_floodCts != null)
            return;

        _floodCts = new CancellationTokenSource();
        var token = _floodCts.Token;
        _startFloodButton.Enabled = false;
        _stopFloodButton.Enabled = true;

        _floodTask = Task.Run(async () =>
        {
            var i = 0;
            while (!token.IsCancellationRequested)
            {
                var id = i++;
                Interlocked.Increment(ref _incomingCount);

                using (_logger.BeginScope(new Dictionary<string, object?>
                       {
                           ["Flow"] = "Flood",
                           ["Batch"] = id / 1000,
                           ["TraceScopeId"] = id % 50
                       }))
                {
                    _logger.ZLogInformation($"flood index={id}, traceId={Guid.NewGuid()}, payloadSize={id % 10}");
                }
                if (id % 10 == 0)
                    _logger.LogWarning("Warning sample at {Index}", id);
                if (id % 41 == 0)
                    _logger.LogError(new InvalidOperationException("Simulated exception"), "Failure at {Index}", id);

                if (id % 1000 == 0)
                {
                    await Task.Delay(5, token).ConfigureAwait(false);
                }
            }
        }, token);
    }

    private async Task StopFloodAsync()
    {
        if (_floodCts == null)
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
                // Expected.
            }
        }

        _floodTask = null;
        _floodCts.Dispose();
        _floodCts = null;
        _startFloodButton.Enabled = true;
        _stopFloodButton.Enabled = false;
    }

    private void ApplyFilter()
    {
        var options = new LogFilterOptions
        {
            TextContains = string.IsNullOrWhiteSpace(_searchTextBox.Text) ? null : _searchTextBox.Text,
            MatchCase = false,
            AllowedLevels = ParseLevelFilter(_levelFilterCombo.SelectedItem?.ToString())
        };

        _controller.ApplyFilter(options);
    }

    private static HashSet<LogSeverity> ParseLevelFilter(string? selected)
    {
        if (string.IsNullOrWhiteSpace(selected) || selected == "All")
            return new HashSet<LogSeverity>();

        return Enum.TryParse<LogSeverity>(selected, out var parsed)
            ? new HashSet<LogSeverity> { parsed }
            : new HashSet<LogSeverity>();
    }

    private static ClearMode ParseClearMode(string? selected)
        => Enum.TryParse<ClearMode>(selected, out var parsed) ? parsed : ClearMode.Fast;

    private void FindNext()
    {
        var pattern = _searchTextBox.Text;
        if (string.IsNullOrWhiteSpace(pattern))
            return;

        _controller.FindNext(pattern, matchCase: false, useRegex: false);
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

        var incomingRate = incoming - Interlocked.Exchange(ref _lastIncomingCount, incoming);
        var renderedRate = rendered - Interlocked.Exchange(ref _lastRenderedCount, rendered);
        var lineCount = _viewerHost.HostControl is ScintillaNET.Scintilla scintilla ? scintilla.Lines.Count : 0;
        var process = Process.GetCurrentProcess();
        var workingSetMb = process.WorkingSet64 / 1024d / 1024d;
        var privateMb = process.PrivateMemorySize64 / 1024d / 1024d;
        var managedMb = GC.GetTotalMemory(false) / 1024d / 1024d;

        _metricsLabel.Text =
            $"incoming/s: {incomingRate:n0} | rendered/s: {renderedRate:n0} | dropped(local): {dropped:n0} | " +
            $"attempted: {attempted:n0} | accepted: {accepted:n0} | localFail: {localFail:n0} | " +
            $"dropEst: {dropEstimate:n0} | backlog(est): {backlogEstimate:n0} | " +
            $"history: {_controller.HistoryEntries:n0} | lines: {lineCount:n0} | " +
            $"ws: {workingSetMb:n1} MB | private: {privateMb:n1} MB | managed: {managedMb:n1} MB";
    }
}
