using CommunityToolkit.Mvvm.ComponentModel;
using DevTools.ModelsContext.Diagnostics;
using DevTools.ModelsContext.Engine;
using DevTools.ModelsContext.Model;
using System.Globalization;

namespace DevTools.ModelsContext.ViewModels;

internal sealed record FilterJob(string Name, HostFilterInfo? Host, FilterDefinition Definition)
{
    public bool IsEmptyCustom => Host is null && Definition.Groups.Count == 0 && Definition.Root.Children.Count == 0;
}

public sealed partial class FilterRunner : ObservableObject, IDisposable
{
    public const int LivePauseLimit = 5000;

    public static readonly TimeSpan LiveDelay = TimeSpan.FromMilliseconds(300);

    private readonly Func<FilterJob?> _job;
    private readonly Debouncer _live;
    private readonly PanelRuntime _runtime;
    private HashSet<ElementRef> _baseline = [];
    private ContextData _data = ContextData.Empty;
    private CancellationTokenSource? _evaluation;
    private bool _isLiveOn = true;
    private CancellationToken _token;

    internal FilterRunner(PanelRuntime runtime, Func<FilterJob?> job)
    {
        _runtime = runtime;
        _job = job;
        _live = new Debouncer(runtime.Scheduler, LiveDelay, () => _ = _runtime.RunAsync(() => EvaluateAsync(true)));
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResultCount))]
    [NotifyPropertyChangedFor(nameof(IsLivePaused))]
    [NotifyPropertyChangedFor(nameof(IsSyncing))]
    public partial IReadOnlyList<ElementRef> Result { get; private set; } = [];

    public int ResultCount => Result.Count;

    [ObservableProperty]
    public partial ResultSelectionMode Mode { get; set; }

    public bool IsLiveOn
    {
        get => _isLiveOn;
        set
        {
            if (!SetProperty(ref _isLiveOn, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsLivePaused));
            OnPropertyChanged(nameof(IsSyncing));
            if (value)
            {
                _ = _runtime.RunAsync(CaptureBaselineAsync);
            }
        }
    }

    public bool IsLivePaused => IsLiveOn && ResultCount > LivePauseLimit;

    public bool IsSyncing => IsLiveOn && !IsLivePaused;

    public static string LivePausedText { get; } =
        string.Format(CultureInfo.CurrentCulture, "Paused: over {0:N0} matches", LivePauseLimit);

    public void Dispose()
    {
        _live.Dispose();
        _evaluation?.Cancel();
        _evaluation?.Dispose();
    }

    internal void Load(ContextData data, CancellationToken cancellationToken)
    {
        _data = data;
        _token = cancellationToken;
    }

    internal void Trigger()
    {
        _live.Trigger();
    }

    internal async Task ActivateAsync()
    {
        if (IsLiveOn)
        {
            await CaptureBaselineAsync();
        }
    }

    internal void Reset(bool isLiveOn)
    {
        _evaluation?.Cancel();
        _live.Cancel();
        _data = ContextData.Empty;
        if (_isLiveOn != isLiveOn)
        {
            _isLiveOn = isLiveOn;
            OnPropertyChanged(nameof(IsLiveOn));
            OnPropertyChanged(nameof(IsLivePaused));
            OnPropertyChanged(nameof(IsSyncing));
        }

        Result = [];
    }

    internal async Task EvaluateAsync(bool sync)
    {
        _evaluation?.Cancel();
        _evaluation?.Dispose();
        _evaluation = CancellationTokenSource.CreateLinkedTokenSource(_token);
        var token = _evaluation.Token;
        var timer = TraceTimer.Start(_runtime.Logger);
        var result = await ResultAsync(_job(), token);
        token.ThrowIfCancellationRequested();
        var job = _job();
        var name = job?.Name ?? "(none)";
        if (timer.IsEnabled)
        {
            _runtime.Logger.FilterEvaluated(name, result.Count, timer.ElapsedMs);
        }

        Result = result;
        if (!sync || !IsLiveOn || job is null || job.IsEmptyCustom)
        {
            return;
        }

        if (IsLivePaused)
        {
            _runtime.Logger.FilterLivePaused(name, result.Count, LivePauseLimit);
            return;
        }

        IReadOnlyCollection<ElementRef> selection = Mode switch
        {
            ResultSelectionMode.Add => _baseline.Union(result).ToList(),
            ResultSelectionMode.Remove => _baseline.Except(result).ToList(),
            _ => result
        };
        await _runtime.SelectAsync(selection, token);
    }

    partial void OnModeChanged(ResultSelectionMode value)
    {
        _live.Trigger();
    }

    private async Task CaptureBaselineAsync()
    {
        var selection = await _runtime.Services.Selection.GetSelectionAsync(_token);
        _baseline = [.. selection];
    }

    private async Task<IReadOnlyList<ElementRef>> ResultAsync(FilterJob? job, CancellationToken cancellationToken)
    {
        if (job is null)
        {
            return [];
        }

        if (job.Host is { } host)
        {
            return await HostResultAsync(job.Name, host, cancellationToken);
        }

        return job.IsEmptyCustom ? [] : await CustomResultAsync(job, cancellationToken);
    }

    private async Task<IReadOnlyList<ElementRef>> HostResultAsync(string name, HostFilterInfo host,
        CancellationToken cancellationToken)
    {
        if (_data.Find(host.Document) is not { } scope)
        {
            return [];
        }

        var timer = TraceTimer.Start(_runtime.Logger);
        var matches = await _runtime.Services.HostFilters.EvaluateAsync(scope.Key, host.Id, scope.Ids,
            cancellationToken);
        var found = matches.Where(scope.InScope).Select(id => new ElementRef(scope.Key, id)).ToList();
        if (timer.IsEnabled)
        {
            _runtime.Logger.FilterDocumentEvaluated(name, scope.Info.Source.Label, found.Count, timer.ElapsedMs);
        }

        return found;
    }

    private async Task<IReadOnlyList<ElementRef>> CustomResultAsync(FilterJob job, CancellationToken cancellationToken)
    {
        var definition = job.Definition;
        var result = new List<ElementRef>();
        foreach (var document in _data.Documents)
        {
            if (FilterScopes.ForDocument(definition.Root, document.Info.Source, _data.KnownSources) is not { } root)
            {
                continue;
            }

            var timer = TraceTimer.Start(_runtime.Logger);
            var ids = await _runtime.Services.Evaluator.EvaluateAsync(document.Key, definition with { Root = root },
                document.Ids, cancellationToken);
            if (timer.IsEnabled)
            {
                _runtime.Logger.FilterDocumentEvaluated(job.Name, document.Info.Source.Label, ids.Count, timer.ElapsedMs);
            }

            result.AddRange(ids.Select(id => new ElementRef(document.Key, id)));
        }

        return result;
    }
}
