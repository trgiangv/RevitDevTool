using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Tests.Shared;
using DevTools.ModelsContext.ViewModels;
using DevTools.Presentation.Views.ModelsContext;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace DevTools.ModelsContext.Sandbox;

internal enum SandboxSize
{
    Hundred,
    Million
}

internal sealed class SandboxRun : IDisposable
{
    private readonly Dispatcher _dispatcher;

    private SandboxRun(SandboxSize size, FakeModelData data, FakeModel model, ModelContextViewModel panel, Dispatcher dispatcher)
    {
        Size = size;
        Data = data;
        Model = model;
        Panel = panel;
        _dispatcher = dispatcher;
    }

    public SandboxSize Size { get; }

    public FakeModelData Data { get; }

    public FakeModel Model { get; }

    public ModelContextViewModel Panel { get; }

    public static FakeModelData BuildData(SandboxSize size)
    {
        if (size == SandboxSize.Hundred)
        {
            return FakeModelGenerator.Generate(new FakeModelOptions { CurrentElements = 100_000, LinkElements = 20_000 });
        }

        var data = FakeModelGenerator.Generate(new FakeModelOptions { CurrentElements = 300_000, LinkElements = 175_000 });
        var fifth = new DocumentInfo(
            DocumentKey.Link(FakeModelGenerator.ModelId, 9004),
            FakeModelGenerator.MepSource,
            "MEP-Mech.rvt",
            true,
            data.Documents[1].Capabilities);
        return data with { Documents = [.. data.Documents, fifth] };
    }

    public static SandboxRun Create(SandboxSize size, TimeSpan delay, Dispatcher dispatcher, int extraParameters = 0)
    {
        var data = BuildData(size);
        var model = new FakeModel(data) { ExtraParameters = extraParameters };
        var source = SlowSource.Wrap(model, () => delay);
        var services = new FakeHost(model).Services(new SystemClock(), new DispatcherScheduler(dispatcher),
            ConsoleLog.Current, source);
        var panel = new ModelContextViewModel(services);
        panel.ContextBar.Scope = ScopeKind.WholeModel;
        return new SandboxRun(size, data, model, panel, dispatcher);
    }

    public static int CountContainers(DependencyObject node)
    {
        var count = node is ListBoxItem ? 1 : 0;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(node); index++)
        {
            count += CountContainers(VisualTreeHelper.GetChild(node, index));
        }

        return count;
    }

    public async Task<IReadOnlyList<string>> MeasureAsync(ModelContextView view, Action<string> log)
    {
        var lines = new List<string>();
        void Report(string text)
        {
            lines.Add(text);
            log(text);
        }

        var stall = new StallMeter(_dispatcher);
        stall.Start();
        var explorer = Panel.Explorer;
        var watch = Stopwatch.StartNew();
        double? firstRows = null;
        explorer.Rows.CollectionChanged += (_, _) => firstRows ??= explorer.Rows.Count > 0 ? watch.Elapsed.TotalMilliseconds : null;

        await Panel.InitializeAsync(CancellationToken.None);
        var rowsMs = firstRows ?? watch.Elapsed.TotalMilliseconds;
        await Idle();
        var renderedMs = watch.Elapsed.TotalMilliseconds;
        Report($"first rows {rowsMs:F0} ms (rendered {renderedMs:F0} ms), rows {explorer.Rows.Count}, longest stall {stall.TakeMax():F0} ms");

        watch.Restart();
        Panel.ContextBar.Tick(Data.Documents.Select(document => document.Key));
        await Panel.RefreshAsync();
        await Idle();
        Report($"all {Data.Documents.Count} documents ticked {watch.Elapsed.TotalMilliseconds:F0} ms, rows {explorer.Rows.Count}, longest stall {stall.TakeMax():F0} ms");

        var type = await FindTypeAsync(explorer);
        stall.TakeMax();
        watch.Restart();
        await explorer.ExpandAsync(type);
        var modelMs = watch.Elapsed.TotalMilliseconds;
        await Idle();
        var expandMs = watch.Elapsed.TotalMilliseconds;
        var shown = type.Children!.Count(child => child.Kind == TreeRowKind.Instance);
        Report($"expand {shown} instances {expandMs:F0} ms (view model {modelMs:F0} ms, layout {expandMs - modelMs:F0} ms), longest stall {stall.TakeMax():F0} ms");
        explorer.Collapse(type);
        await Idle();
        var second = await FindTypeAsync(explorer, type);
        stall.TakeMax();
        watch.Restart();
        await explorer.ExpandAsync(second);
        modelMs = watch.Elapsed.TotalMilliseconds;
        await Idle();
        expandMs = watch.Elapsed.TotalMilliseconds;
        Report($"expand again {expandMs:F0} ms (view model {modelMs:F0} ms, layout {expandMs - modelMs:F0} ms), longest stall {stall.TakeMax():F0} ms");

        var current = explorer.Rows.First(row => row.Kind == TreeRowKind.CurrentModelHeader);
        var category = explorer.Rows.First(row => row.Kind == TreeRowKind.Group);
        watch.Restart();
        foreach (var action in new[] { MenuAction.Isolate, MenuAction.Halftone, MenuAction.Color })
        {
            explorer.ReasonFor(category, action);
        }

        var hoverMs = watch.Elapsed.TotalMilliseconds;
        watch.Restart();
        explorer.OpenMenu(current);
        explorer.OpenMenu(category);
        Report($"row tools on hover {hoverMs:F1} ms, open menu twice {watch.Elapsed.TotalMilliseconds:F1} ms");

        var resets = 0;
        void OnRows(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => resets++;
        explorer.Rows.CollectionChanged += OnRows;
        await Idle();
        stall.TakeMax();
        foreach (var row in explorer.Rows.Where(row => row.Kind == TreeRowKind.Group).Take(5).ToList())
        {
            explorer.SetSelectedRows([row]);
            await Task.Delay(400);
            await Idle();
        }

        explorer.Rows.CollectionChanged -= OnRows;
        Report($"click 5 rows with sync: row list rebuilt {resets} times, longest stall {stall.TakeMax():F0} ms");

        var list = Find<DevTools.UI.Controls.FlatTreeList>(view);
        Report($"realized row containers {CountContainers(list)} of {explorer.Rows.Count} rows");
        stall.Stop();
        return lines;
    }

    public void Dispose()
    {
        Panel.Dispose();
    }

    private static T Find<T>(DependencyObject node)
        where T : DependencyObject
    {
        return FindOrDefault<T>(node) ?? throw new InvalidOperationException(typeof(T).Name + " was not found.");
    }
    private static T? FindOrDefault<T>(DependencyObject node)
        where T : DependencyObject
    {
        if (node is T match)
        {
            return match;
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(node); index++)
        {
            if (FindOrDefault<T>(VisualTreeHelper.GetChild(node, index)) is { } found)
            {
                return found;
            }
        }

        return default;
    }

    private async Task<TreeRow> FindTypeAsync(ExplorerTabViewModel explorer, TreeRow? skip = null)
    {
        var current = explorer.Rows.First(row => row.Kind == TreeRowKind.CurrentModelHeader);
        foreach (var group in current.Children!.ToList())
        {
            await explorer.ExpandAsync(group);
            foreach (var family in group.Children!.ToList())
            {
                await explorer.ExpandAsync(family);
                var type = family.Children!.FirstOrDefault(child =>
                    child.Count >= ExplorerTabViewModel.InstancePageSize && child != skip);
                if (type is not null)
                {
                    return type;
                }
            }
        }

        throw new InvalidOperationException("No type with a full instance page.");
    }

    private Task Idle()
    {
        return _dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle).Task;
    }
}

internal sealed class StallMeter
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(10);
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = new();
    private double _last;
    private double _max;

    public StallMeter(Dispatcher dispatcher)
    {
        _timer = new DispatcherTimer(DispatcherPriority.Normal, dispatcher) { Interval = Interval };
        _timer.Tick += (_, _) =>
        {
            var now = _clock.Elapsed.TotalMilliseconds;
            _max = Math.Max(_max, now - _last - Interval.TotalMilliseconds);
            _last = now;
        };
    }

    public void Start()
    {
        _clock.Restart();
        _last = 0;
        _max = 0;
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
    }

    public double TakeMax()
    {
        var max = Math.Max(0, _max);
        _max = 0;
        _last = _clock.Elapsed.TotalMilliseconds;
        return max;
    }
}
