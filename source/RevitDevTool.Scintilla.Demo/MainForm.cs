using System.Diagnostics;
using System.Text;
using Autodesk.Revit.DB;
using Microsoft.Extensions.Logging;
using RevitDevTool.Scintilla.Control;
using RevitDevTool.Scintilla.Core;
using RevitDevTool.Scintilla.Render;
using RevitDevTool.Scintilla.Search;
using ZLogger;

namespace RevitDevTool.Scintilla.Demo;

internal sealed class MainForm : Form
{
    private readonly ScintillaLogViewer _viewer;
    private readonly ILogViewerController _controller;
    private readonly ILogViewerControlEvents _controlEvents;
    private readonly ILogger<MainForm> _logger;
    private readonly DemoEnrichmentCallbacks _enrichmentCallbacks;
    private readonly System.Windows.Forms.Timer _metricsTimer;

    private readonly Button _startFloodButton = new() { Text = "Start Flood", Width = 110 };
    private readonly Button _stopFloodButton = new() { Text = "Stop Flood", Width = 110, Enabled = false };
    private readonly Button _clearButton = new() { Text = "Clear", Width = 80 };
    private readonly Button _emitSamplesButton = new() { Text = "Emit Samples", Width = 110 };
    private readonly ComboBox _clearModeCombo = new() { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _searchTextBox = new() { Width = 240, PlaceholderText = "Search..." };
    private readonly Button _findNextButton = new() { Text = "Find Next", Width = 100 };
    private readonly Button _findPreviousButton = new() { Text = "Find Previous", Width = 110 };
    private readonly ComboBox _levelFilterCombo = new() { Width = 130, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox _prettyJsonCheckBox = new() { AutoSize = true, Text = "PrettyJson", Checked = true };
    private readonly CheckBox _tokenCallbackCheckBox = new() { AutoSize = true, Text = "Token Callback", Checked = true };
    private readonly ComboBox _themeCombo = new() { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Label _searchStatusLabel = new() { AutoSize = true, Text = "matches: 0" };
    private readonly Label _metricsLabel = new() { AutoSize = true };
    private readonly System.Windows.Forms.Timer _searchDebounceTimer = new() { Interval = 180 };

    private CancellationTokenSource? _floodCts;
    private CancellationTokenSource? _searchCountCts;
    private Task? _floodTask;
    private long _incomingCount;
    private long _lastIncomingCount;
    private long _lastRenderedCount;
    private bool _smokeMode;

    public MainForm(
        ScintillaLogViewer viewer,
        ILogViewerControlEvents controlEvents,
        ILogger<MainForm> logger,
        DemoEnrichmentCallbacks enrichmentCallbacks)
    {
        Text = "RevitDevTool.Scintilla Demo (ZLogger)";
        Width = 1450;
        Height = 900;

        _viewer = viewer;
        _controller = _viewer.Controller;
        _controlEvents = controlEvents;
        _logger = logger;
        _enrichmentCallbacks = enrichmentCallbacks;

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
        _searchDebounceTimer.Stop();
        _searchCountCts?.Cancel();
        _searchCountCts?.Dispose();
        _searchCountCts = null;
        _controlEvents.RenderModeChanged -= OnRenderModeChanged;
        _controlEvents.ThemeChanged -= OnThemeChanged;
        StopFloodAsync().GetAwaiter().GetResult();
        _controlEvents.RequestStop();
        _viewer.Dispose();
        base.OnFormClosing(e);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _controlEvents.RequestStart();
        if (_smokeMode)
            _ = RunSmokeScenarioAsync();
    }

    internal void EnableSmokeMode() => _smokeMode = true;

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
            nameof(LogLevel.Trace),
            nameof(LogLevel.Debug),
            nameof(LogLevel.Information),
            nameof(LogLevel.Warning),
            nameof(LogLevel.Error),
            nameof(LogLevel.Critical)
        });
        _levelFilterCombo.SelectedIndex = 0;
        _themeCombo.Items.AddRange(new object[] { "Dark", "Light" });
        _themeCombo.SelectedItem = "Dark";
        _clearModeCombo.Items.AddRange(new object[]
        {
            nameof(ClearMode.Fast),
            nameof(ClearMode.Aggressive)
        });
        _clearModeCombo.SelectedIndex = 0;

        toolbar.Controls.Add(_startFloodButton);
        toolbar.Controls.Add(_stopFloodButton);
        toolbar.Controls.Add(_clearButton);
        toolbar.Controls.Add(_emitSamplesButton);
        toolbar.Controls.Add(new Label { Text = "Clear:", AutoSize = true, Margin = new Padding(8, 8, 0, 0) });
        toolbar.Controls.Add(_clearModeCombo);
        toolbar.Controls.Add(new Label { Text = "Level:", AutoSize = true, Margin = new Padding(12, 8, 0, 0) });
        toolbar.Controls.Add(_levelFilterCombo);
        toolbar.Controls.Add(new Label { Text = "Render:", AutoSize = true, Margin = new Padding(12, 8, 0, 0) });
        toolbar.Controls.Add(_prettyJsonCheckBox);
        toolbar.Controls.Add(_tokenCallbackCheckBox);
        toolbar.Controls.Add(new Label { Text = "Theme:", AutoSize = true, Margin = new Padding(12, 8, 0, 0) });
        toolbar.Controls.Add(_themeCombo);
        toolbar.Controls.Add(new Label { Text = "Search:", AutoSize = true, Margin = new Padding(12, 8, 0, 0) });
        toolbar.Controls.Add(_searchTextBox);
        toolbar.Controls.Add(_findNextButton);
        toolbar.Controls.Add(_findPreviousButton);
        toolbar.Controls.Add(_searchStatusLabel);
        toolbar.Controls.Add(new Label { Text = "  ", AutoSize = true });
        toolbar.Controls.Add(_metricsLabel);

        Controls.Add(_viewer.HostControl);
        Controls.Add(toolbar);
    }

    private void HookEvents()
    {
        _controlEvents.RenderModeChanged += OnRenderModeChanged;
        _controlEvents.ThemeChanged += OnThemeChanged;

        _startFloodButton.Click += (_, _) => StartFlood();
        _stopFloodButton.Click += async (_, _) => await StopFloodAsync();
        _clearButton.Click += (_, _) =>
        {
            _controlEvents.RequestClear(ParseClearMode(_clearModeCombo.SelectedItem?.ToString()));
            Interlocked.Exchange(ref _incomingCount, 0);
        };
        _emitSamplesButton.Click += (_, _) => EmitShowcaseLogs();
        _findNextButton.Click += (_, _) => FindNext();
        _findPreviousButton.Click += (_, _) => FindPrevious();
        _searchTextBox.KeyDown += (_, args) =>
        {
            if (args.KeyCode == Keys.Enter)
            {
                _searchDebounceTimer.Stop();
                FindNext();
                args.Handled = true;
                args.SuppressKeyPress = true;
            }
        };
        _searchDebounceTimer.Tick += (_, _) =>
        {
            _searchDebounceTimer.Stop();
            HighlightSearchVisible();
        };
        _searchTextBox.TextChanged += (_, _) =>
        {
            _searchDebounceTimer.Stop();
            _searchDebounceTimer.Start();
        };
        _levelFilterCombo.SelectedIndexChanged += (_, _) => ApplyFilter();
        _prettyJsonCheckBox.CheckedChanged += (_, _) =>
        {
            _controlEvents.RequestRenderMode(_prettyJsonCheckBox.Checked);
            ApplyFilter();
        };
        _tokenCallbackCheckBox.CheckedChanged += (_, _) =>
        {
            _enrichmentCallbacks.EnableTokenResolution = _tokenCallbackCheckBox.Checked;
        };
        _themeCombo.SelectedIndexChanged += (_, _) =>
        {
            var isLight = string.Equals(_themeCombo.SelectedItem?.ToString(), "Light", StringComparison.OrdinalIgnoreCase);
            var theme = isLight ? ScintillaTheme.EnhancedLight : ScintillaTheme.EnhancedDark;
            _controlEvents.RequestTheme(theme);
        };
    }

    private void SeedInitialLogs()
    {
        for (var i = 0; i < 100; i++)
        {
            var elementId = new ElementId(i + 1000);
            using var _ = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["Session"] = "Warmup",
                ["WarmupIndex"] = i,
                ["ElementId"] = elementId
            });
            _logger.ZLogInformation(
                $"Aurecon warmup log line index={i}, elementId={elementId}, uniqueId={"8d2f3f31-2b9f-4f6e-a18d-3e7a8f3b1c2d-0001a2b3"}, ifcGuid={"2Zw$n7f8AJvQk4r7sP9mA$"}, url={"https://example.com/revit"}");
        }
        _logger.ZLogInformation($"{new
        {
            eventName = "warmup",
            state = "ok",
            count = 100,
            nested = new { pretty = true }
        }}");
        EmitShowcaseLogs();
    }

    private void EmitShowcaseLogs()
    {
        _logger.ZLogInformation($"=== LOG LEVEL DETECTION TESTS ===");
        _logger.ZLogInformation($"[INFO] This should be Information level");
        _logger.ZLogWarning($"[WARN] This should be Warning level");
        _logger.ZLogError($"[ERROR] This should be Error level");
        _logger.ZLogCritical($"[FATAL] This should be Critical level");
        _logger.ZLogDebug($"[DEBUG] This should be Debug level");

        _logger.ZLogInformation($"Operation completed successfully");
        _logger.ZLogWarning($"Warning: Memory usage is high");
        _logger.ZLogError($"Error occurred during processing");
        _logger.ZLogCritical($"Fatal crash detected in system");
        _logger.ZLogDebug($"Just a regular debug message");

        _logger.ZLogInformation($"=== TRACE TESTS ===");
        _logger.ZLogInformation($"Plain INFO message");
        _logger.ZLogWarning($"Plain WARNING message");
        _logger.ZLogError($"Plain ERROR message");

        _logger.ZLogInformation($"Cache hit ratio: {0.856:P2}");
        _logger.ZLogInformation($"Response time: {DateTime.Now}");
        _logger.ZLogInformation($"Is valid: {true}");
        _logger.ZLogInformation($"API version: {"2.1.0"}");
        _logger.ZLogWarning($"Memory usage: {1024} MB (threshold: {2048} MB)");
        _logger.ZLogError($"Failed order {"ORD-12345"} with code {"E500"}");

        var simpleMetrics = new { CPU = 85.5, Memory = 1024, Connections = 42 };
        _logger.ZLogInformation($"{simpleMetrics}");

        var apiRequest = new
        {
            Method = "POST",
            Endpoint = "/api/users",
            RequestId = Guid.NewGuid(),
            Headers = new { ContentType = "application/json", Authorization = "Bearer ***" }
        };
        _logger.ZLogInformation($"{apiRequest}");

        var userAction = new
        {
            UserId = "user123",
            Action = "Login",
            IP = "192.168.1.1",
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
            Timestamp = DateTime.UtcNow
        };
        _logger.ZLogInformation($"{userAction}");

        var config = new
        {
            ConnectionTimeout = 30,
            MaxRetries = 3,
            EnableCompression = true,
            AllowedOrigins = new[] { "https://api.example.com", "https://admin.example.com" }
        };
        _logger.ZLogInformation($"{config}");

        var linkConfig = new
        {
            DocsUrl = "https://api.example.com/docs/revit?tab=viewer",
            SupportMail = "mailto:support@example.com"
        };
        _logger.ZLogInformation($"{linkConfig}");

        var classPayload = new DemoStructuredPayload
        {
            UserId = "class-user-01",
            Action = "SyncModel",
            DocsUrl = "https://learn.microsoft.com/dotnet/",
            SupportMail = "mailto:class-support@example.com",
            RetryCount = 3,
            TimestampUtc = DateTime.UtcNow,
            Details = new DemoStructuredDetails
            {
                HostName = "rvt-host-01",
                Environment = "Production",
                Thresholds = new[] { 512, 1024, 2048 }
            }
        };
        _logger.ZLogInformation($"{classPayload}");

        var classPayload2 = new DemoStructuredPayload
        {
            UserId = "class-user-02",
            Action = "ValidateModel",
            DocsUrl = "https://api.example.com/docs/validator",
            SupportMail = "mailto:validator-support@example.com",
            RetryCount = 1,
            TimestampUtc = DateTime.UtcNow,
            Details = new DemoStructuredDetails
            {
                HostName = "rvt-host-02",
                Environment = "Staging",
                Thresholds = new[] { 256, 768, 1536 }
            }
        };
        _logger.ZLogInformation($"multi-object sample: primary={classPayload}; secondary={classPayload2}");

        var auditLog = new
        {
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            User = new { Id = "user456", Role = "Administrator", Department = "IT" },
            Action = "ConfigurationUpdate",
            Changes = new object[]
            {
                new { Property = "MaxConnections", OldValue = 100, NewValue = 200 },
                new { Property = "Timeout", OldValue = 30, NewValue = 60 }
            }
        };
        _logger.ZLogInformation($"{auditLog}");

        var deploymentInfo = new
        {
            DeploymentId = Guid.NewGuid(),
            Environment = "Production",
            Version = "2.1.0",
            Timestamp = DateTime.UtcNow,
            Services = new object[]
            {
                new
                {
                    Name = "API",
                    Status = "Healthy",
                    Metrics = new { ResponseTime = 45, ErrorRate = 0.01, RequestsPerSecond = 150 }
                },
                new
                {
                    Name = "Database",
                    Status = "Degraded",
                    Metrics = new { ConnectionCount = 85, QueryTime = 120, ReplicationLag = 5 }
                }
            },
            Infrastructure = new
            {
                Region = "us-east-1",
                InstanceType = "t3.large",
                Scaling = new { MinInstances = 2, MaxInstances = 5, CurrentInstances = 3 }
            }
        };
        _logger.ZLogInformation($"{deploymentInfo}");

        _logger.ZLogDebug($"=== DEBUG TESTS ===");
        _logger.ZLogDebug($"Plain debug message");
        var debugQuery = new
        {
            Sql = "SELECT * FROM Users WHERE Status = @status",
            Parameters = new { status = "Active" },
            ExecutionTime = 150
        };
        _logger.ZLogDebug($"{debugQuery}");

        _logger.ZLogInformation($"=== CONSOLE TESTS ===");
        _logger.ZLogInformation($"Plain console message");
        var consoleMetrics = new { Uptime = TimeSpan.FromHours(48.5), Requests = 1000000 };
        _logger.ZLogInformation($"{consoleMetrics}");

        _logger.ZLogDebug($"=== EXCEPTION TEST ===");
        try
        {
            throw new InvalidOperationException("Test exception", new Exception("Inner exception"));
        }
        catch (Exception ex)
        {
            _logger.ZLogError($"Exception caught: {ex.Message}");
            _logger.ZLogDebug($"{ex}");
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

                using var _ = _logger.BeginScope(new Dictionary<string, object?>
                {
                    ["Flow"] = "Flood",
                    ["Batch"] = id / 1000,
                    ["TraceScopeId"] = id % 50,
                    ["ElementId"] = id % 200_000
                });
                _logger.ZLogInformation(
                    $"flood index={id}, traceId={Guid.NewGuid()}, payloadSize={id % 10}, uniqueId={"8d2f3f31-2b9f-4f6e-a18d-3e7a8f3b1c2d-0001a2b3"}, ifcGuid={"2Zw$n7f8AJvQk4r7sP9mA$"}, url={"https://example.com/revit"}");
                if (id % 10 == 0)
                {
                    _logger.ZLogWarning($"Warning sample at index={id}");
                }
                if (id % 41 == 0)
                {
                    _logger.ZLogError($"Failure at index={id}, error={"Simulated exception"}");
                }

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
            // Search box is find/highlight only. Keep filter state independent from search text
            // so typing does not rebuild visible document.
            TextContains = null,
            MatchCase = false,
            AllowedLevels = ParseLevelFilter(_levelFilterCombo.SelectedItem?.ToString())
        };

        _controlEvents.RequestFilter(options);
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

    private void FindNext()
    {
        RequestSearch(searchBackward: false);
    }

    private void FindPrevious()
    {
        RequestSearch(searchBackward: true);
    }

    private void RequestSearch(bool searchBackward)
    {
        var pattern = _searchTextBox.Text;
        if (string.IsNullOrWhiteSpace(pattern))
            return;

        _controlEvents.RequestSearch(new LogSearchRequest
        {
            Pattern = pattern,
            MatchCase = false,
            UseRegex = false,
            SearchBackward = searchBackward
        });
    }

    private void HighlightSearchVisible()
    {
        var pattern = _searchTextBox.Text ?? string.Empty;
        _controlEvents.RequestSearch(new LogSearchRequest
        {
            Pattern = pattern,
            MatchCase = false,
            UseRegex = false,
            HighlightOnly = true
        });
        ScheduleTotalMatchCount(pattern);
    }

    private void ScheduleTotalMatchCount(string pattern)
    {
        _searchCountCts?.Cancel();
        _searchCountCts?.Dispose();
        _searchCountCts = null;

        if (string.IsNullOrWhiteSpace(pattern))
        {
            _searchStatusLabel.Text = "matches: 0";
            return;
        }

        _searchStatusLabel.Text = "matches: ...";
        var cts = new CancellationTokenSource();
        _searchCountCts = cts;
        _ = Task.Run(async () =>
        {
            try
            {
                // Small delay to avoid recount bursts while user edits text.
                await Task.Delay(120, cts.Token).ConfigureAwait(false);
                var total = await _controller.CountMatchesAsync(pattern, matchCase: false, useRegex: false, cts.Token).ConfigureAwait(false);
                if (cts.IsCancellationRequested)
                    return;

                if (IsDisposed)
                    return;

                BeginInvoke(new Action(() =>
                {
                    if (!IsDisposed && _searchCountCts == cts)
                        _searchStatusLabel.Text = $"matches: {total:n0}";
                }));
            }
            catch (OperationCanceledException)
            {
                // Expected when search text changes rapidly.
            }
        }, cts.Token);
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
        var lineCount = _viewer.HostControl is ScintillaNET.Scintilla scintilla ? scintilla.Lines.Count : 0;
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

    private async Task RunSmokeScenarioAsync()
    {
        try
        {
            StartFlood();
            await Task.Delay(1800).ConfigureAwait(true);

            _levelFilterCombo.SelectedItem = nameof(LogLevel.Error);
            ApplyFilter();
            await Task.Delay(600).ConfigureAwait(true);

            await StopFloodAsync().ConfigureAwait(true);
            var linesAfterFlood = GetCurrentLineCount();

            _controlEvents.RequestClear(ClearMode.Fast);
            await WaitForBacklogDrainedAsync(1200).ConfigureAwait(true);
            var linesAfterClear = GetCurrentLineCount();

            using var _ = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["SmokeScope"] = "scope-filter"
            });
            _logger.ZLogError($"SMOKE scope-filter validation id={Guid.NewGuid()}");
            await Task.Delay(300).ConfigureAwait(true);

            _levelFilterCombo.SelectedItem = nameof(LogLevel.Error);
            _searchTextBox.Text = "scope-filter";
            ApplyFilter();
            await Task.Delay(400).ConfigureAwait(true);
            var linesAfterScopeFilter = GetCurrentLineCount();

            var detectorChecksOk = RunDetectorAssertions(out var detectorReason);
            var ok = linesAfterFlood > 0 && linesAfterClear <= 1 && linesAfterScopeFilter > 0 && detectorChecksOk;
            Console.WriteLine(
                $"SMOKE RESULT: {(ok ? "PASS" : "FAIL")} | floodLines={linesAfterFlood} | clearLines={linesAfterClear} | scopeFilterLines={linesAfterScopeFilter} | detectorChecks={detectorChecksOk} | detector={detectorReason}");
            Environment.ExitCode = ok ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SMOKE RESULT: FAIL | exception={ex}");
            Environment.ExitCode = 1;
        }
        finally
        {
            Close();
        }
    }

    private int GetCurrentLineCount()
        => _viewer.HostControl is ScintillaNET.Scintilla scintilla ? scintilla.Lines.Count : 0;

    private async Task WaitForBacklogDrainedAsync(int timeoutMs)
    {
        var started = Environment.TickCount64;
        while (Environment.TickCount64 - started < timeoutMs)
        {
            if (_controller.IngestBacklogEstimate <= 0)
            {
                await Task.Delay(120).ConfigureAwait(true);
                return;
            }

            await Task.Delay(80).ConfigureAwait(true);
        }
    }

    private static bool RunDetectorAssertions(out string reason)
    {
        reason = "ok";
        var callbacks = new DemoEnrichmentCallbacks { EnableTokenResolution = true, EnablePrettyJson = false };
        var options = new ScintillaLogViewerOptions
        {
            EnrichmentCallbacks = callbacks,
            TokenClassifier = new DemoRevitTokenClassifier(),
            EnablePrettyJson = false
        };
        var strategy = new LogRenderStrategy(
            "Cascadia Mono",
            10,
            new StaticLogThemeProvider(ScintillaTheme.EnhancedDark),
            DefaultLogStyleRegistry.Instance,
            options);

        if (!HasContiguousLinkSpan(strategy, "Open https://example.com/revit/docs?x=1&y=2#part, now", "https://example.com/revit/docs?x=1&y=2#part"))
        {
            reason = "url-span";
            return false;
        }

        if (!HasAtLeastLinkSpanCount(strategy, "Failed order ORD-12345 with code E500 and docs https://learn.microsoft.com/dotnet.", 3))
        {
            reason = "token-precedence";
            return false;
        }

        if (!HasObjectLiteralStringStyle(strategy, "{ CPU = 85.5, Memory = 1024, Name = test-service }"))
        {
            reason = "object-style";
            return false;
        }

        return true;
    }

    private static bool HasContiguousLinkSpan(LogRenderStrategy strategy, string message, string expectedLink)
    {
        var segments = new List<RenderSegment>(32);
        strategy.BuildSegments(CreateLogEntry(message), segments);
        var expectedBytes = Encoding.UTF8.GetByteCount(expectedLink);
        for (var i = 0; i < segments.Count; i++)
        {
            if (segments[i].IsLink && segments[i].Utf8Length == expectedBytes)
                return true;
        }

        return false;
    }

    private static bool HasAtLeastLinkSpanCount(LogRenderStrategy strategy, string message, int expectedMinimum)
    {
        var segments = new List<RenderSegment>(64);
        strategy.BuildSegments(CreateLogEntry(message), segments);
        var count = 0;
        for (var i = 0; i < segments.Count; i++)
        {
            if (segments[i].IsLink)
                count++;
        }

        return count >= expectedMinimum;
    }

    private static bool HasObjectLiteralStringStyle(LogRenderStrategy strategy, string message)
    {
        var segments = new List<RenderSegment>(64);
        strategy.BuildSegments(CreateLogEntry(message), segments);

        var hasJsonString = false;
        for (var i = 0; i < segments.Count; i++)
        {
            if (segments[i].SemanticStyle == LogSemanticStyle.JsonString)
                hasJsonString = true;

            if (segments[i].SemanticStyle == LogSemanticStyle.LevelInformation)
                return false;
        }

        return hasJsonString;
    }

    private static LogEntry CreateLogEntry(string message)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        return new LogEntry
        {
            TimestampUtc = DateTime.UtcNow,
            Level = LogLevel.Information,
            Source = "Smoke",
            Message = new ArraySegment<byte>(bytes),
            Properties = new Dictionary<string, object?>()
        };
    }

}
