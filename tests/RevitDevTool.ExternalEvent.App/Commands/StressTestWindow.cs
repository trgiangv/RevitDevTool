#if DEBUG
using System.Windows;
using System.Windows.Controls;
using Button = System.Windows.Controls.Button;
using FontFamily = System.Windows.Media.FontFamily;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;

namespace RevitDevTool.ExternalEvent.App.Commands;

internal sealed class StressTestWindow : Window
{
    private readonly IReadOnlyList<IDispatchAdapter> _dispatchers;
    private readonly IReadOnlyList<IFixedEventAdapter> _fixedAdapters;
    private readonly TextBox _output;
    private readonly TextBox _requestCountBox;
    private readonly TextBox _producerCountBox;
    private StressTestRunner? _runner;
    private bool _running;

    public StressTestWindow(
        IReadOnlyList<IDispatchAdapter> dispatchers,
        IReadOnlyList<IFixedEventAdapter> fixedAdapters)
    {
        _dispatchers = dispatchers;
        _fixedAdapters = fixedAdapters;

        Title = "ExternalEvent Benchmark";
        Width = 900;
        Height = 700;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var root = new DockPanel { Margin = new Thickness(8) };

        var configPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        DockPanel.SetDock(configPanel, Dock.Top);

        configPanel.Children.Add(Lbl("Requests:"));
        _requestCountBox = new TextBox { Text = "1000", Width = 60, Margin = new Thickness(0, 0, 8, 0) };
        configPanel.Children.Add(_requestCountBox);

        configPanel.Children.Add(Lbl("Producers:"));
        _producerCountBox = new TextBox { Text = "4", Width = 40 };
        configPanel.Children.Add(_producerCountBox);

        root.Children.Add(configPanel);

        var suite1Panel = new WrapPanel { Margin = new Thickness(0, 0, 0, 2) };
        DockPanel.SetDock(suite1Panel, Dock.Top);
        suite1Panel.Children.Add(SectionLbl("Dispatcher:"));
        AddBtn(suite1Panel, "Sequential", RunSequentialLatency);
        AddBtn(suite1Panel, "Seq+Read", RunSequentialLatencyLightRead);
        AddBtn(suite1Panel, "ProducerSeq", RunProducerSequential);
        AddBtn(suite1Panel, "TrueBurst", RunTrueBurst);
        AddBtn(suite1Panel, "Sustained", RunSustainedLoad);
        AddBtn(suite1Panel, "DirectInvoke", RunDirectInvocation);
        AddBtn(suite1Panel, "Reentry", RunNestedReentry);
        AddBtn(suite1Panel, "Cancel", RunCancellationLifecycle);
        AddBtn(suite1Panel, "Errors", RunErrorPropagation);
        AddBtn(suite1Panel, "FIFO", RunFifoOrder);
        AddBtn(suite1Panel, "GC", RunGcPressure);
        root.Children.Add(suite1Panel);

        var suite2Panel = new WrapPanel { Margin = new Thickness(0, 0, 0, 2) };
        DockPanel.SetDock(suite2Panel, Dock.Top);
        suite2Panel.Children.Add(SectionLbl("Fixed Event:"));
        AddBtn(suite2Panel, "SeqRaise", RunFixedSequentialRaise);
        AddBtn(suite2Panel, "DirectInvoke", RunFixedDirectInvocation);
        AddBtn(suite2Panel, "Concurrent", RunFixedConcurrentRaise);
        AddBtn(suite2Panel, "GC", RunFixedGcPressure);
        root.Children.Add(suite2Panel);

        var controlPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
        DockPanel.SetDock(controlPanel, Dock.Top);

        var runAll = new Button
        {
            Content = "Run All",
            Padding = new Thickness(10, 4, 10, 4),
            Margin = new Thickness(4),
            FontWeight = FontWeights.Bold,
        };
        runAll.Click += async (_, _) => await GuardedRun(RunAllTests);
        controlPanel.Children.Add(runAll);

        var stop = new Button { Content = "Stop", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(4) };
        stop.Click += (_, _) => _runner?.Cancel();
        controlPanel.Children.Add(stop);

        var clear = new Button { Content = "Clear", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(4) };
        clear.Click += (_, _) => _output!.Clear();
        controlPanel.Children.Add(clear);

        root.Children.Add(controlPanel);

        _output = new TextBox
        {
            IsReadOnly = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            TextWrapping = TextWrapping.NoWrap,
            AcceptsReturn = true,
        };
        root.Children.Add(_output);

        Content = root;
    }

    private static TextBlock Lbl(string text) =>
        new() { Text = text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 2, 0) };

    private static TextBlock SectionLbl(string text) =>
        new() { Text = text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 4, 0), FontWeight = FontWeights.SemiBold };

    private void AddBtn(WrapPanel panel, string text, Func<Task> handler)
    {
        var btn = new Button { Content = text, Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(2) };
        btn.Click += async (_, _) => await GuardedRun(handler);
        panel.Children.Add(btn);
    }

    private async Task GuardedRun(Func<Task> handler)
    {
        if (_running) return;
        _running = true;
        try { await handler(); }
        catch (Exception ex) { Log($"ERROR: {ex.Message}"); }
        finally { _running = false; }
    }

    private void Log(string message)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => Log(message));
            return;
        }
        _output.AppendText(message + Environment.NewLine);
        _output.ScrollToEnd();
    }

    private (int requestCount, int producerCount) GetConfig()
    {
        int.TryParse(_requestCountBox.Text, out var req);
        int.TryParse(_producerCountBox.Text, out var prod);
        return (Math.Max(req, 10), Math.Max(prod, 1));
    }

    private StressTestRunner NewRunner()
    {
        _runner = new StressTestRunner(Log);
        return _runner;
    }

    private async Task RunSequentialLatency()
    {
        var (req, _) = GetConfig();
        var r = NewRunner();
        foreach (var a in _dispatchers) await r.RunSequentialLatency(a, req);
    }

    private async Task RunSequentialLatencyLightRead()
    {
        var (req, _) = GetConfig();
        var r = NewRunner();
        foreach (var a in _dispatchers)
            await r.RunSequentialLatencyWithWorkload(a, Math.Min(req, 200), WorkloadProfile.LightRevitRead);
    }

    private async Task RunProducerSequential()
    {
        var (req, prod) = GetConfig();
        var r = NewRunner();
        foreach (var a in _dispatchers) await r.RunProducerSequential(a, req, prod);
    }

    private async Task RunTrueBurst()
    {
        var (req, prod) = GetConfig();
        var r = NewRunner();
        foreach (var a in _dispatchers) await r.RunTrueBurst(a, req, Math.Min(prod * 2, 16));
    }

    private async Task RunSustainedLoad()
    {
        var (_, prod) = GetConfig();
        var r = NewRunner();
        foreach (var a in _dispatchers) await r.RunSustainedLoad(a, 5, prod);
    }

    private async Task RunDirectInvocation()
    {
        var (req, _) = GetConfig();
        var r = NewRunner();
        foreach (var a in _dispatchers) await r.RunDirectInvocation(a, Math.Min(req, 100));
    }

    private async Task RunNestedReentry()
    {
        var r = NewRunner();
        foreach (var a in _dispatchers) await r.RunNestedReentry(a, 50);
    }

    private async Task RunCancellationLifecycle()
    {
        var r = NewRunner();
        foreach (var a in _dispatchers) await r.RunCancellationLifecycle(a, 100);
    }

    private async Task RunErrorPropagation()
    {
        var r = NewRunner();
        foreach (var a in _dispatchers) await r.RunErrorPropagation(a, 50);
    }

    private async Task RunFifoOrder()
    {
        var r = NewRunner();
        foreach (var a in _dispatchers) await r.RunFifoOrder(a, 200);
    }

    private async Task RunGcPressure()
    {
        var (req, _) = GetConfig();
        var r = NewRunner();
        foreach (var a in _dispatchers) await r.RunGcPressure(a, Math.Min(req, 500));
    }

    private async Task RunFixedSequentialRaise()
    {
        var (req, _) = GetConfig();
        var r = NewRunner();
        foreach (var a in _fixedAdapters) await r.RunFixedSequentialRaise(a, req);
    }

    private async Task RunFixedDirectInvocation()
    {
        var (req, _) = GetConfig();
        var r = NewRunner();
        foreach (var a in _fixedAdapters) await r.RunFixedDirectInvocation(a, Math.Min(req, 100));
    }

    private async Task RunFixedConcurrentRaise()
    {
        var (req, prod) = GetConfig();
        var r = NewRunner();
        foreach (var a in _fixedAdapters) await r.RunFixedConcurrentRaise(a, req, Math.Min(prod * 2, 16));
    }

    private async Task RunFixedGcPressure()
    {
        var (req, _) = GetConfig();
        var r = NewRunner();
        foreach (var a in _fixedAdapters) await r.RunFixedGcPressure(a, Math.Min(req, 500));
    }

    private async Task RunAllTests()
    {
        var (req, prod) = GetConfig();
        var r = NewRunner();
        await r.RunAll(_dispatchers, _fixedAdapters, req, prod);
    }
}
#endif
