#if DEBUG
using System.Windows;
using System.Windows.Controls;
using Button = System.Windows.Controls.Button;
using ComboBox = System.Windows.Controls.ComboBox;
using FontFamily = System.Windows.Media.FontFamily;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;

namespace RevitDevTool.ExternalEvent.App.Commands;

internal sealed class StressTestWindow : Window
{
    private readonly IReadOnlyList<IDispatchAdapter> _adapters;
    private readonly TextBox _output;
    private readonly ComboBox _libSelector;
    private readonly TextBox _requestCountBox;
    private readonly TextBox _producerCountBox;
    private StressTestRunner? _runner;
    private bool _running;

    public StressTestWindow(IReadOnlyList<IDispatchAdapter> adapters)
    {
        _adapters = adapters;

        Title = "ExternalEvent Stress Test";
        Width = 800;
        Height = 600;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var root = new DockPanel { Margin = new Thickness(8) };

        var configPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        DockPanel.SetDock(configPanel, Dock.Top);

        configPanel.Children.Add(new TextBlock { Text = "Library: ", VerticalAlignment = VerticalAlignment.Center });
        _libSelector = new ComboBox { Width = 180, Margin = new Thickness(0, 0, 8, 0) };
        _libSelector.Items.Add("All");
        foreach (var adapter in adapters) _libSelector.Items.Add(adapter.Name);
        _libSelector.SelectedIndex = 0;
        configPanel.Children.Add(_libSelector);

        configPanel.Children.Add(new TextBlock { Text = "Requests: ", VerticalAlignment = VerticalAlignment.Center });
        _requestCountBox = new TextBox { Text = "1000", Width = 60, Margin = new Thickness(0, 0, 8, 0) };
        configPanel.Children.Add(_requestCountBox);

        configPanel.Children.Add(new TextBlock { Text = "Producers: ", VerticalAlignment = VerticalAlignment.Center });
        _producerCountBox = new TextBox { Text = "4", Width = 40, Margin = new Thickness(0, 0, 8, 0) };
        configPanel.Children.Add(_producerCountBox);

        root.Children.Add(configPanel);

        var buttonPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        DockPanel.SetDock(buttonPanel, Dock.Top);

        AddButton(buttonPanel, "Overhead Profile", RunOverheadProfile);
        AddButton(buttonPanel, "Throughput", RunThroughput);
        AddButton(buttonPanel, "Burst", RunBurst);
        AddButton(buttonPanel, "FIFO Order", RunFifoOrder);
        AddButton(buttonPanel, "Cancellation", RunCancellation);
        AddButton(buttonPanel, "Mixed Workload", RunMixedWorkload);
        AddButton(buttonPanel, "Run All", RunAllTests);

        var cancelBtn = new Button { Content = "Cancel", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(4) };
        cancelBtn.Click += (_, _) => _runner?.Cancel();
        buttonPanel.Children.Add(cancelBtn);

        var clearBtn = new Button { Content = "Clear", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(4) };
        clearBtn.Click += (_, _) => _output!.Clear();
        buttonPanel.Children.Add(clearBtn);

        root.Children.Add(buttonPanel);

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

    private void AddButton(WrapPanel panel, string text, Func<Task> handler)
    {
        var btn = new Button { Content = text, Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(4) };
        btn.Click += async (_, _) =>
        {
            if (_running) return;
            _running = true;
            try { await handler(); }
            catch (Exception ex) { Log($"ERROR: {ex.Message}"); }
            finally { _running = false; }
        };
        panel.Children.Add(btn);
    }

    private void Log(string message)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => Log(message));
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

    private IReadOnlyList<IDispatchAdapter> GetSelectedAdapters()
    {
        if (_libSelector.SelectedIndex == 0) return _adapters;
        var selected = _adapters[_libSelector.SelectedIndex - 1];
        return [selected];
    }

    private async Task RunOverheadProfile()
    {
        var (req, _) = GetConfig();
        foreach (var adapter in GetSelectedAdapters())
        {
            _runner = new StressTestRunner(Log);
            await _runner.RunOverheadProfile(adapter, req);
        }
    }

    private async Task RunThroughput()
    {
        var (req, prod) = GetConfig();
        foreach (var adapter in GetSelectedAdapters())
        {
            _runner = new StressTestRunner(Log);
            await _runner.RunThroughput(adapter, req, prod);
        }
    }

    private async Task RunBurst()
    {
        var (req, prod) = GetConfig();
        foreach (var adapter in GetSelectedAdapters())
        {
            _runner = new StressTestRunner(Log);
            await _runner.RunBurst(adapter, req, Math.Min(prod * 2, 16));
        }
    }

    private async Task RunFifoOrder()
    {
        foreach (var adapter in GetSelectedAdapters())
        {
            _runner = new StressTestRunner(Log);
            await _runner.RunFifoOrder(adapter, 200);
        }
    }

    private async Task RunCancellation()
    {
        foreach (var adapter in GetSelectedAdapters())
        {
            _runner = new StressTestRunner(Log);
            await _runner.RunCancellation(adapter, 100);
        }
    }

    private async Task RunMixedWorkload()
    {
        var (req, _) = GetConfig();
        foreach (var adapter in GetSelectedAdapters())
        {
            _runner = new StressTestRunner(Log);
            await _runner.RunMixedWorkload(adapter, Math.Min(req, 500), 4);
        }
    }

    private async Task RunAllTests()
    {
        var (req, prod) = GetConfig();
        _runner = new StressTestRunner(Log);
        await _runner.RunAll(GetSelectedAdapters(), req, prod);
    }
}
#endif
