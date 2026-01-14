using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Sinks.RichTextBoxForms.Themes;
using System.Diagnostics;
using ZLogger.RichTextBox.Winforms;
using ZLogger.RichTextBox.Winforms.Themes;
using WinFormsRichTextBox = System.Windows.Forms.RichTextBox;

namespace ZLogger.RichTextBox.Winforms.Benchmark;

/// <summary>
/// Interactive stress test application for visual comparison.
/// Run this to see real-time performance differences.
/// </summary>
public partial class InteractiveStressTestForm : Form
{
    private WinFormsRichTextBox _serilogRtb = null!;
    private WinFormsRichTextBox _zloggerRtb = null!;
    private Label _serilogStats = null!;
    private Label _zloggerStats = null!;
    private NumericUpDown _messageCount = null!;
    private Button _runButton = null!;
    private Button _clearButton = null!;
    private ComboBox _testType = null!;
    private ProgressBar _progress = null!;

    private Serilog.ILogger? _serilogLogger;
    private Serilog.Sinks.RichTextBoxForms.RichTextBoxSink? _serilogSink;
    private Microsoft.Extensions.Logging.ILogger? _zloggerLogger;
    private ZLoggerRichTextBoxLoggerProvider? _zloggerProvider;
    private ILoggerFactory? _zloggerFactory;

    public InteractiveStressTestForm()
    {
        InitializeComponents();
        InitializeLoggers();
    }

    private void InitializeComponents()
    {
        Text = "ZLogger vs Serilog - Interactive Stress Test";
        Size = new Size(1400, 800);
        StartPosition = FormStartPosition.CenterScreen;

        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(10)
        };
        mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));

        // Headers
        var serilogHeader = new Label
        {
            Text = "Serilog.Sinks.RichTextBox.WinForms.Colored",
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill,
            ForeColor = Color.DarkBlue
        };

        var zloggerHeader = new Label
        {
            Text = "ZLogger.RichTextBox.Winforms",
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill,
            ForeColor = Color.DarkGreen
        };

        // RichTextBoxes
        _serilogRtb = new WinFormsRichTextBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Cascadia Mono", 9),
            ReadOnly = true,
            BackColor = Color.Black,
            ForeColor = Color.White
        };

        _zloggerRtb = new WinFormsRichTextBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Cascadia Mono", 9),
            ReadOnly = true,
            BackColor = Color.Black,
            ForeColor = Color.White
        };

        // Control panel
        var controlPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(5)
        };

        controlPanel.Controls.Add(new Label { Text = "Messages:", AutoSize = true, Margin = new Padding(5, 8, 5, 0) });
        _messageCount = new NumericUpDown
        {
            Minimum = 100,
            Maximum = 100000,
            Value = 10000,
            Increment = 1000,
            Width = 100
        };
        controlPanel.Controls.Add(_messageCount);

        controlPanel.Controls.Add(new Label { Text = "Test:", AutoSize = true, Margin = new Padding(15, 8, 5, 0) });
        _testType = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 150
        };
        _testType.Items.AddRange(new object[]
        {
            "Sequential",
            "Parallel.For",
            "Task.Run",
            "Mixed Levels",
            "With Exceptions",
            "Complex Objects"
        });
        _testType.SelectedIndex = 0;
        controlPanel.Controls.Add(_testType);

        _runButton = new Button
        {
            Text = "▶ Run Test",
            Width = 100,
            Height = 30,
            Margin = new Padding(20, 3, 5, 3),
            BackColor = Color.LimeGreen
        };
        _runButton.Click += RunTest;
        controlPanel.Controls.Add(_runButton);

        _clearButton = new Button
        {
            Text = "Clear",
            Width = 80,
            Height = 30,
            Margin = new Padding(5, 3, 5, 3)
        };
        _clearButton.Click += ClearLogs;
        controlPanel.Controls.Add(_clearButton);

        _progress = new ProgressBar
        {
            Width = 200,
            Height = 25,
            Margin = new Padding(20, 5, 5, 5),
            Style = ProgressBarStyle.Marquee,
            Visible = false
        };
        controlPanel.Controls.Add(_progress);

        // Stats panel
        var statsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        statsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        statsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        _serilogStats = new Label
        {
            Text = "Ready",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Consolas", 10),
            ForeColor = Color.DarkBlue
        };

        _zloggerStats = new Label
        {
            Text = "Ready",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Consolas", 10),
            ForeColor = Color.DarkGreen
        };

        statsPanel.Controls.Add(_serilogStats, 0, 0);
        statsPanel.Controls.Add(_zloggerStats, 1, 0);

        // Layout
        mainPanel.Controls.Add(serilogHeader, 0, 0);
        mainPanel.Controls.Add(zloggerHeader, 1, 0);
        mainPanel.Controls.Add(_serilogRtb, 0, 1);
        mainPanel.Controls.Add(_zloggerRtb, 1, 1);

        var bottomPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1
        };
        bottomPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        bottomPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        bottomPanel.Controls.Add(controlPanel, 0, 0);
        bottomPanel.Controls.Add(statsPanel, 0, 1);

        mainPanel.Controls.Add(bottomPanel, 0, 2);
        mainPanel.SetColumnSpan(bottomPanel, 2);

        Controls.Add(mainPanel);
    }

    private void InitializeLoggers()
    {
        // Serilog
        _serilogLogger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.RichTextBox(_serilogRtb, out _serilogSink,
                theme: Serilog.Sinks.RichTextBoxForms.Themes.ThemePresets.Literate,
                autoScroll: true,
                maxLogLines: 1000)
            .CreateLogger();

        // ZLogger
        _zloggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
            builder.AddZLoggerRichTextBox(_zloggerRtb, out _zloggerProvider, options =>
            {
                options.Theme = ZLogger.RichTextBox.Winforms.Themes.ThemePresets.Literate;
                options.AutoScroll = true;
                options.MaxLogLines = 1000;
            });
        });
        _zloggerLogger = _zloggerFactory.CreateLogger("Benchmark");
    }

    private async void RunTest(object? sender, EventArgs e)
    {
        var count = (int)_messageCount.Value;
        var testType = _testType.SelectedIndex;

        _runButton.Enabled = false;
        _progress.Visible = true;

        var serilogSw = new Stopwatch();
        var zloggerSw = new Stopwatch();

        try
        {
            await Task.Run(() =>
            {
                // Run Serilog test
                serilogSw.Start();
                RunSerilogTest(testType, count);
                serilogSw.Stop();

                // Small pause between tests
                Thread.Sleep(100);

                // Run ZLogger test
                zloggerSw.Start();
                RunZLoggerTest(testType, count);
                zloggerSw.Stop();
            });

            // Update stats
            UpdateStats(serilogSw.Elapsed, zloggerSw.Elapsed, count);
        }
        finally
        {
            _runButton.Enabled = true;
            _progress.Visible = false;
        }
    }

    private void RunSerilogTest(int testType, int count)
    {
        switch (testType)
        {
            case 0: // Sequential
                for (var i = 0; i < count; i++)
                    _serilogLogger!.Information("Message {Index}", i);
                break;

            case 1: // Parallel.For
                Parallel.For(0, count, i =>
                    _serilogLogger!.Information("Parallel {Thread} - {Index}", Environment.CurrentManagedThreadId, i));
                break;

            case 2: // Task.Run
                var tasks = new List<Task>();
                for (var t = 0; t < 10; t++)
                {
                    var taskId = t;
                    tasks.Add(Task.Run(() =>
                    {
                        for (var i = 0; i < count / 10; i++)
                            _serilogLogger!.Information("Task {TaskId} - {Index}", taskId, i);
                    }));
                }
                Task.WaitAll(tasks.ToArray());
                break;

            case 3: // Mixed Levels
                for (var i = 0; i < count; i++)
                {
                    switch (i % 6)
                    {
                        case 0: _serilogLogger!.Verbose("Trace {Index}", i); break;
                        case 1: _serilogLogger!.Debug("Debug {Index}", i); break;
                        case 2: _serilogLogger!.Information("Info {Index}", i); break;
                        case 3: _serilogLogger!.Warning("Warning {Index}", i); break;
                        case 4: _serilogLogger!.Error("Error {Index}", i); break;
                        case 5: _serilogLogger!.Fatal("Critical {Index}", i); break;
                    }
                }
                break;

            case 4: // With Exceptions
                var ex = new InvalidOperationException("Test", new ArgumentException("Inner"));
                for (var i = 0; i < count; i++)
                    _serilogLogger!.Error(ex, "Exception at {Index}", i);
                break;

            case 5: // Complex Objects
                var data = new { Id = Guid.NewGuid(), Time = DateTime.UtcNow, Values = new[] { 1, 2, 3 } };
                for (var i = 0; i < count; i++)
                    _serilogLogger!.Information("Data: {@Data} at {Index}", data, i);
                break;
        }
    }

    private void RunZLoggerTest(int testType, int count)
    {
        switch (testType)
        {
            case 0: // Sequential
                for (var i = 0; i < count; i++)
                    _zloggerLogger!.LogInformation("Message {Index}", i);
                break;

            case 1: // Parallel.For
                Parallel.For(0, count, i =>
                    _zloggerLogger!.LogInformation("Parallel {Thread} - {Index}", Environment.CurrentManagedThreadId, i));
                break;

            case 2: // Task.Run
                var tasks = new List<Task>();
                for (var t = 0; t < 10; t++)
                {
                    var taskId = t;
                    tasks.Add(Task.Run(() =>
                    {
                        for (var i = 0; i < count / 10; i++)
                            _zloggerLogger!.LogInformation("Task {TaskId} - {Index}", taskId, i);
                    }));
                }
                Task.WaitAll(tasks.ToArray());
                break;

            case 3: // Mixed Levels
                for (var i = 0; i < count; i++)
                {
                    switch (i % 6)
                    {
                        case 0: _zloggerLogger!.LogTrace("Trace {Index}", i); break;
                        case 1: _zloggerLogger!.LogDebug("Debug {Index}", i); break;
                        case 2: _zloggerLogger!.LogInformation("Info {Index}", i); break;
                        case 3: _zloggerLogger!.LogWarning("Warning {Index}", i); break;
                        case 4: _zloggerLogger!.LogError("Error {Index}", i); break;
                        case 5: _zloggerLogger!.LogCritical("Critical {Index}", i); break;
                    }
                }
                break;

            case 4: // With Exceptions
                var ex = new InvalidOperationException("Test", new ArgumentException("Inner"));
                for (var i = 0; i < count; i++)
                    _zloggerLogger!.LogError(ex, "Exception at {Index}", i);
                break;

            case 5: // Complex Objects
                var data = new { Id = Guid.NewGuid(), Time = DateTime.UtcNow, Values = new[] { 1, 2, 3 } };
                for (var i = 0; i < count; i++)
                    _zloggerLogger!.LogInformation("Data: {Data} at {Index}", data, i);
                break;
        }
    }

    private void UpdateStats(TimeSpan serilogTime, TimeSpan zloggerTime, int count)
    {
        var serilogOps = count / serilogTime.TotalSeconds;
        var zloggerOps = count / zloggerTime.TotalSeconds;
        var ratio = serilogOps > 0 ? zloggerOps / serilogOps : 0;

        Invoke(() =>
        {
            _serilogStats.Text = $"Time: {serilogTime.TotalMilliseconds:F1}ms | {serilogOps:N0} ops/sec";
            _zloggerStats.Text = $"Time: {zloggerTime.TotalMilliseconds:F1}ms | {zloggerOps:N0} ops/sec | {ratio:F2}x";

            _zloggerStats.ForeColor = ratio >= 1 ? Color.Green : Color.Red;
        });
    }

    private void ClearLogs(object? sender, EventArgs e)
    {
        _serilogSink?.Clear();
        _zloggerProvider?.Processor.Clear();
        _serilogStats.Text = "Ready";
        _zloggerStats.Text = "Ready";
        _zloggerStats.ForeColor = Color.DarkGreen;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        (_serilogLogger as IDisposable)?.Dispose();
        _serilogSink?.Dispose();
        _zloggerProvider?.Dispose();
        _zloggerFactory?.Dispose();
        base.OnFormClosing(e);
    }
}

/// <summary>
/// Entry point for interactive test - run with: dotnet run --project ... -- --interactive
/// </summary>
public static class InteractiveTestRunner
{
    public static void Run()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new InteractiveStressTestForm());
    }
}
