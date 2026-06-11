#if DEBUG
using System.Text;
using System.Windows;
using System.Windows.Controls;
using MdXaml;
using Button = System.Windows.Controls.Button;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;

namespace RevitDevTool.ExternalEvent.App.Commands;

internal sealed class StressTestWindow : Window
{
    private readonly IReadOnlyList<IDispatchAdapter> _dispatchers;
    private readonly IReadOnlyList<IInContextEventAdapter> _inContextAdapters;
    private readonly MarkdownScrollViewer _mdViewer;
    private readonly StringBuilder _mdBuffer = new();
    private readonly TextBox _requestCountBox;
    private readonly TextBox _producerCountBox;
    private StressTestRunner? _runner;
    private bool _running;

    public StressTestWindow(
        IReadOnlyList<IDispatchAdapter> dispatchers,
        IReadOnlyList<IInContextEventAdapter> inContextAdapters)
    {
        _dispatchers = dispatchers;
        _inContextAdapters = inContextAdapters;

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
        AddBtn(suite1Panel, "Seq+Tx", RunSequentialLatencyTransaction);
        AddBtn(suite1Panel, "ProducerSeq", RunProducerSequential);
        AddBtn(suite1Panel, "TrueBurst", RunTrueBurst);
        AddBtn(suite1Panel, "Sustained", RunSustainedLoad);
        AddBtn(suite1Panel, "DirectInvoke", RunDirectInvocation);
        AddBtn(suite1Panel, "Reentry", RunNestedReentry);
        AddBtn(suite1Panel, "Cancel", RunCancellationLifecycle);
        AddBtn(suite1Panel, "Errors", RunErrorPropagation);
        AddBtn(suite1Panel, "FIFO", RunFifoOrder);
        root.Children.Add(suite1Panel);

        var suite2Panel = new WrapPanel { Margin = new Thickness(0, 0, 0, 2) };
        DockPanel.SetDock(suite2Panel, Dock.Top);
        suite2Panel.Children.Add(SectionLbl("In-Context:"));
        AddBtn(suite2Panel, "SeqRaise", RunInContextSequentialRaise);
        AddBtn(suite2Panel, "DirectInvoke", RunInContextDirectInvocation);
        AddBtn(suite2Panel, "Concurrent", RunInContextConcurrentRaise);
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
        clear.Click += (_, _) => { _mdBuffer.Clear(); _mdViewer!.Markdown = ""; };
        controlPanel.Children.Add(clear);

        root.Children.Add(controlPanel);

        _mdViewer = new MarkdownScrollViewer
        {
            Markdown = "",
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        root.Children.Add(_mdViewer);

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
        _mdBuffer.AppendLine(message);
        _mdViewer.Markdown = _mdBuffer.ToString();
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

    private async Task ForEachDispatcher(Func<StressTestRunner, IDispatchAdapter, Task> action)
    {
        var r = NewRunner();
        foreach (var a in _dispatchers) await action(r, a);
    }

    private async Task ForEachInContext(Func<StressTestRunner, IInContextEventAdapter, Task> action)
    {
        var r = NewRunner();
        foreach (var a in _inContextAdapters) await action(r, a);
    }

    private async Task RunSequentialLatency()
    {
        var (req, _) = GetConfig();
        await ForEachDispatcher((r, a) => r.RunSequentialLatency(a, req));
    }

    private async Task RunSequentialLatencyLightRead()
    {
        var (req, _) = GetConfig();
        await ForEachDispatcher((r, a) => r.RunSequentialLatency(a, Math.Min(req, 200), WorkloadProfile.LightRevitRead));
    }

    private async Task RunSequentialLatencyTransaction()
    {
        var (req, _) = GetConfig();
        await ForEachDispatcher((r, a) => r.RunSequentialLatency(a, Math.Min(req, 50), WorkloadProfile.TransactionRollback));
    }

    private async Task RunProducerSequential()
    {
        var (req, prod) = GetConfig();
        await ForEachDispatcher((r, a) => r.RunProducerSequential(a, req, prod));
    }

    private async Task RunTrueBurst()
    {
        var (req, prod) = GetConfig();
        await ForEachDispatcher((r, a) => r.RunTrueBurst(a, req, Math.Min(prod * 2, 16)));
    }

    private async Task RunSustainedLoad()
    {
        var (_, prod) = GetConfig();
        await ForEachDispatcher((r, a) => r.RunSustainedLoad(a, 5, prod));
    }

    private async Task RunDirectInvocation()
    {
        var (req, _) = GetConfig();
        await ForEachDispatcher((r, a) => r.RunDirectInvocation(a, Math.Min(req, 100)));
    }

    private async Task RunNestedReentry() =>
        await ForEachDispatcher((r, a) => r.RunNestedReentry(a, 50));

    private async Task RunCancellationLifecycle() =>
        await ForEachDispatcher((r, a) => r.RunCancellationLifecycle(a, 100));

    private async Task RunErrorPropagation() =>
        await ForEachDispatcher((r, a) => r.RunErrorPropagation(a, 50));

    private async Task RunFifoOrder() =>
        await ForEachDispatcher((r, a) => r.RunFifoOrder(a, 200));

    private async Task RunInContextSequentialRaise()
    {
        var (req, _) = GetConfig();
        await ForEachInContext((r, a) => r.RunInContextSequentialRaise(a, req));
    }

    private async Task RunInContextDirectInvocation()
    {
        var (req, _) = GetConfig();
        await ForEachInContext((r, a) => r.RunInContextDirectInvocation(a, Math.Min(req, 100)));
    }

    private async Task RunInContextConcurrentRaise()
    {
        var (req, prod) = GetConfig();
        await ForEachInContext((r, a) => r.RunInContextConcurrentRaise(a, req, Math.Min(prod * 2, 16)));
    }

    private async Task RunAllTests()
    {
        var (req, prod) = GetConfig();
        var r = NewRunner();
        await r.RunAll(_dispatchers, _inContextAdapters, req, prod);
    }
}
#endif
