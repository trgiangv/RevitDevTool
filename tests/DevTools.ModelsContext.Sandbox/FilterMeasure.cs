using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Parameters;
using DevTools.ModelsContext.Tests.Shared;
using DevTools.Presentation.Views.ModelsContext.Panel;
using DevTools.UI.Controls;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace DevTools.ModelsContext.Sandbox;

internal static class FilterMeasure
{
    public const int ExtraParameters = 1200;

    public static async Task RunAsync(SandboxRun run, ModelContextView view, Dispatcher dispatcher, Action<string> report)
    {
        var stall = new StallMeter(dispatcher);
        stall.Start();
        Task Idle() => dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle).Task;

        await MeasureScopesAsync(run, report);

        var panel = run.Panel;
        panel.IsFilterTab = true;
        var filter = panel.Filter;
        filter.Library.AddCommand.Execute(null);
        await Idle();
        report($"filter parameters {filter.Root.Parameters.Count:N0} merged from {run.Data.Sources.Count} files over {run.Data.Documents.Count} documents");

        filter.Runner.IsLiveOn = true;
        await Idle();
        stall.TakeMax();
        var first = await ResultAsync(filter.Runner, () => FilterExamples.UpperLevelDoorsWithoutMark(filter, FakeParameters.Comments), Idle);
        report($"build example 2 {first.ChangeMs:F0} ms + layout {first.LayoutMs:F0} ms, evaluated {first.Ms:F0} ms (300 ms debounce), {first.Count:N0} matches, longest stall {stall.TakeMax():F0} ms");

        var toggled = await ResultAsync(filter.Runner, () => filter.Root.Children[1].ToggleConnectorCommand.Execute(null), Idle);
        report($"connector toggle to result {toggled.Ms:F0} ms (300 ms debounce), {toggled.Count:N0} matches, longest stall {stall.TakeMax():F0} ms");
        filter.Root.Children[1].ToggleConnectorCommand.Execute(null);

        var watch = Stopwatch.StartNew();
        filter.Root.Children[1].MoveUpCommand.Execute(null);
        await Idle();
        report($"Alt+Up move {watch.Elapsed.TotalMilliseconds:F1} ms, longest stall {stall.TakeMax():F0} ms");

        var fourth = await ResultAsync(filter.Runner, () => FilterExamples.ArchWallsOrMepDucts(filter), Idle);
        report($"example 4 built in {fourth.ChangeMs:F0} ms + layout {fourth.LayoutMs:F0} ms, evaluated {fourth.Ms:F0} ms (300 ms debounce), {fourth.Count:N0} matches, longest stall {stall.TakeMax():F0} ms");
        filter.Runner.IsLiveOn = false;

        await MeasurePickerAsync(filter, view, stall, Idle, report);
        stall.Stop();
    }

    private static async Task MeasureScopesAsync(SandboxRun run, Action<string> report)
    {
        var catalogs = new List<SourceCatalog>();
        foreach (var source in run.Data.Sources.Values)
        {
            catalogs.Add(new SourceCatalog(source.Id,
                await run.Model.GetParameterCatalogAsync(source.Id, CancellationToken.None),
                source.Types.Select(type => type.GroupId).ToHashSet()));
        }

        var scopes = new ParameterScopes();
        scopes.Set(catalogs);
        var documents = run.Data.Sources.Keys.ToList();
        var root = new EffectiveScope(documents, null);
        scopes.For(root);
        var group = root.Narrow(FilterScope.Of([documents[1]], [FilterExamples.Walls]));
        var watch = Stopwatch.StartNew();
        var list = scopes.For(group);
        var cold = watch.Elapsed.TotalMilliseconds;
        watch.Restart();
        var again = scopes.For(group);
        var warm = watch.Elapsed.TotalMilliseconds;
        report($"group parameter list cold {cold:F2} ms / warm {warm:F3} ms (cache hit {ReferenceEquals(list, again)}), {list.Count:N0} of {scopes.All.Count:N0}");

        var tree = FilterSet.Any(
            FilterSet.All(new FilterRule(FakeParameters.FireRating, FilterOperator.Equal, "2h"))
                .In(FilterScope.Of([documents[1]], [FilterExamples.Walls])),
            FilterSet.All(new FilterRule(FakeParameters.Insulation, FilterOperator.Undefined))
                .In(FilterScope.Of([documents[2]], [FilterExamples.Ducts])));
        var known = documents.ToHashSet();
        const int Rounds = 1000;
        watch.Restart();
        for (var round = 0; round < Rounds; round++)
        {
            foreach (var document in documents)
            {
                FilterScopes.ForDocument(tree, document, known);
            }
        }

        report($"scope pruning per document {watch.Elapsed.TotalMilliseconds * 1000 / (Rounds * documents.Count):F2} us");
    }

    private static async Task MeasurePickerAsync(FilterTabViewModel filter, ModelContextView view, StallMeter stall,
        Func<Task> idle, Action<string> report)
    {
        filter.Root.Children.Clear();
        filter.Categories.CheckNoneCommand.Execute(null);
        filter.Root.AddConditionCommand.Execute(null);
        await idle();
        var box = Find<AutoCompleteComboBox>(view);
        FocusManager.SetFocusedElement(Window.GetWindow(box)!, box);
        stall.TakeMax();
        var watch = Stopwatch.StartNew();
        box.IsDropDownOpen = true;
        await idle();
        report($"picker open {watch.Elapsed.TotalMilliseconds:F0} ms over {box.Items.Count:N0} items, longest stall {stall.TakeMax():F0} ms");

        box.Setting = new NoDelaySetting();
        foreach (var text in new[] { "P", "Pa", "Par" })
        {
            await SettleRunnerAsync(idle);
            stall.TakeMax();
            watch.Restart();
            box.Text = text;
            var filterMs = watch.Elapsed.TotalMilliseconds;
            box.UpdateLayout();
            var layoutMs = watch.Elapsed.TotalMilliseconds - filterMs;
            await idle();
            report($"picker keystroke '{text}' open: filter {filterMs:F1} ms + layout {layoutMs:F1} ms + until idle {watch.Elapsed.TotalMilliseconds - filterMs - layoutMs:F0} ms, {box.Items.Count:N0} items, longest stall {stall.TakeMax():F0} ms");
        }

        await MeasureRowChurnAsync(box, stall, idle, report);
        box.IsDropDownOpen = false;
        await idle();
        foreach (var text in new[] { "Q", "Qa", "Qar" })
        {
            stall.TakeMax();
            watch.Restart();
            box.Text = text;
            var filterMs = watch.Elapsed.TotalMilliseconds;
            await idle();
            report($"picker keystroke '{text}' closed: filter {filterMs:F1} ms + layout {watch.Elapsed.TotalMilliseconds - filterMs:F0} ms, longest stall {stall.TakeMax():F0} ms");
        }
    }

    private static async Task SettleRunnerAsync(Func<Task> idle)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(600));
        await idle();
    }

    private static async Task MeasureRowChurnAsync(AutoCompleteComboBox box, StallMeter stall, Func<Task> idle,
        Action<string> report)
    {
        const int Warmup = 4;
        var layouts = new List<double>();
        var totals = new List<double>();
        var stalls = new List<double>();
        var watch = new Stopwatch();
        for (var round = 0; round < Warmup + 20; round++)
        {
            await SettleRunnerAsync(idle);
            stall.TakeMax();
            watch.Restart();
            box.Text = round % 2 == 0 ? "Param 1" : "Param 2";
            box.UpdateLayout();
            var layoutMs = watch.Elapsed.TotalMilliseconds;
            await idle();
            if (round >= Warmup)
            {
                layouts.Add(layoutMs);
                totals.Add(watch.Elapsed.TotalMilliseconds);
                stalls.Add(stall.TakeMax());
            }
        }

        layouts.Sort();
        totals.Sort();
        stalls.Sort();
        report($"picker keystroke replacing every visible row: median filter + layout {layouts[layouts.Count / 2]:F1} ms, until idle {totals[totals.Count / 2]:F0} ms, stall {stalls[stalls.Count / 2]:F0} ms over {totals.Count} keystrokes, {box.Items.Count:N0} items");
    }

    private static async Task<(double Ms, int Count, double ChangeMs, double LayoutMs)> ResultAsync(FilterRunner runner, Action change, Func<Task> idle)
    {
        var done = new TaskCompletionSource<bool>();
        void OnChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FilterRunner.Result))
            {
                done.TrySetResult(true);
            }
        }

        runner.PropertyChanged += OnChanged;
        var watch = Stopwatch.StartNew();
        try
        {
            change();
            var changeMs = watch.Elapsed.TotalMilliseconds;
            Application.Current.MainWindow?.UpdateLayout();
            var layoutMs = watch.Elapsed.TotalMilliseconds - changeMs;
            if (await Task.WhenAny(done.Task, Task.Delay(TimeSpan.FromSeconds(30))) != done.Task)
            {
                throw new TimeoutException("No filter result within 30 s.");
            }

            var ms = watch.Elapsed.TotalMilliseconds;
            await idle();
            return (ms, runner.ResultCount, changeMs, layoutMs);
        }
        finally
        {
            runner.PropertyChanged -= OnChanged;
        }
    }

    private static T Find<T>(DependencyObject node)
        where T : DependencyObject
    {
        var queue = new Queue<DependencyObject>();
        queue.Enqueue(node);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current is T match)
            {
                return match;
            }

            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(current); index++)
            {
                queue.Enqueue(VisualTreeHelper.GetChild(current, index));
            }
        }

        throw new InvalidOperationException(typeof(T).Name + " was not found.");
    }
}

internal sealed class NoDelaySetting : AutoCompleteComboBoxSetting
{
    public override TimeSpan Delay => TimeSpan.Zero;
}
