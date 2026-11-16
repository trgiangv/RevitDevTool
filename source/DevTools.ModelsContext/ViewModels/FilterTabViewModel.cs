using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTools.ModelsContext.Diagnostics;
using DevTools.ModelsContext.Engine;
using DevTools.ModelsContext.Model;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;

namespace DevTools.ModelsContext.ViewModels;

public sealed partial class FilterTabViewModel : ObservableValidator, IFilterEditor, IDisposable
{
    public const int LivePauseLimit = 5000;
    public const string AllCategories = "All categories";

    public static readonly TimeSpan LiveDelay = TimeSpan.FromMilliseconds(300);

    private readonly Debouncer _liveDebouncer;
    private readonly ContextMenuViewModel _menu;
    private readonly PanelRuntime _runtime;
    private readonly VisibilityController _visibility;
    private IReadOnlyList<ParameterInfo> _allParameters = [];
    private HashSet<ElementRef> _baseline = [];
    private IReadOnlyList<(SourceId Source, IReadOnlyList<ParameterInfo> Catalog)> _catalogs = [];
    private FilterSourceItem? _current;
    private ContextData _data = ContextData.Empty;
    private bool _editingSources;
    private CancellationTokenSource? _evaluation;
    private FilterDefinition? _hostDefinition;
    private bool _isLiveOn = true;
    private bool _loading;
    private FilterSourceItem? _pending;
    private FilterState? _restore;
    private CancellationToken _token;

    internal FilterTabViewModel(PanelRuntime runtime, VisibilityController visibility, ContextMenuViewModel menu)
    {
        _runtime = runtime;
        _visibility = visibility;
        _menu = menu;
        Root = new RuleSetViewModel(this, true);
        _liveDebouncer = new Debouncer(runtime.Services.Scheduler, LiveDelay,
            () => _ = _runtime.RunAsync(() => EvaluateAsync(true)));
        ErrorsChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(NameError));
            SaveCommand.NotifyCanExecuteChanged();
        };
    }

    public ObservableCollection<FilterSourceItem> Sources { get; } = [];

    public FilterSourceItem? SelectedSource
    {
        get => _current;
        set
        {
            if (_editingSources || ReferenceEquals(value, _current))
            {
                return;
            }

            if (IsDirty && _current is not null)
            {
                SetPending(value);
                OnPropertyChanged();
                return;
            }

            _ = _runtime.RunAsync(() => SwitchToAsync(value));
        }
    }

    [ObservableProperty]
    public partial bool IsSwitchPending { get; private set; }

    [ObservableProperty]
    public partial bool IsReadOnly { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial bool IsDirty { get; private set; }

    [ObservableProperty]
    public partial string Name { get; private set; } = string.Empty;

    [CustomValidation(typeof(FilterTabViewModel), nameof(CheckName))]
    public string EditName
    {
        get;
        set
        {
            if (SetProperty(ref field, value ?? string.Empty))
            {
                ValidateName();
            }
        }
    } = string.Empty;

    public string? NameError => GetErrors(nameof(EditName)).FirstOrDefault()?.ErrorMessage;

    [ObservableProperty]
    public partial bool IsEditingName { get; private set; }

    public bool CanSave => _current is { IsCustom: true } && IsDirty && NameError is null;

    public ObservableCollection<CategoryItemViewModel> Categories { get; } = [];

    public string CategorySearch
    {
        get;
        set
        {
            if (SetProperty(ref field, value ?? string.Empty))
            {
                UpdateVisibleCategories();
            }
        }
    } = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<CategoryItemViewModel> VisibleCategories { get; private set; } = [];

    public string CategorySummary
    {
        get
        {
            var names = Categories.Where(item => item.IsChecked).Select(item => item.Name).ToList();
            return names.Count == 0 ? AllCategories : string.Join(", ", names);
        }
    }

    [ObservableProperty]
    public partial IReadOnlyList<ParameterInfo> Parameters { get; private set; } = [];

    [ObservableProperty]
    public partial RuleSetViewModel Root { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResultCount))]
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

            IsLivePaused = false;
            OnPropertyChanged(nameof(IsSyncing));
            if (value)
            {
                _ = _runtime.RunAsync(CaptureBaselineAsync);
            }
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSyncing))]
    public partial bool IsLivePaused { get; private set; }

    public bool IsSyncing => IsLiveOn && !IsLivePaused;

    public string LivePausedText => "Live paused · use Select in " + _runtime.Services.Source.HostName;

    public void Dispose()
    {
        _liveDebouncer.Dispose();
        _evaluation?.Cancel();
        _evaluation?.Dispose();
    }

    public void BeginRename()
    {
        if (_current is not { IsCustom: true })
        {
            return;
        }

        IsEditingName = true;
        EditName = Name;
        ValidateName();
    }

    public bool CommitRename()
    {
        if (!IsEditingName)
        {
            return true;
        }

        ValidateName();
        if (NameError is not null)
        {
            return false;
        }

        var trimmed = EditName.Trim();
        IsEditingName = false;
        EditName = trimmed;
        if (!string.Equals(trimmed, Name, StringComparison.Ordinal))
        {
            Name = trimmed;
            if (_current is { IsCustom: true } item)
            {
                item.Name = trimmed;
            }

            IsDirty = true;
        }

        return true;
    }

    public void CancelRename()
    {
        IsEditingName = false;
        EditName = Name;
        ClearErrors(nameof(EditName));
    }

    public void LeaveRename()
    {
        if (!CommitRename())
        {
            CancelRename();
        }
    }

    public static ValidationResult? CheckName(string name, ValidationContext context)
    {
        var error = FilterNames.Validate(name, ((FilterTabViewModel)context.ObjectInstance).OtherNames());
        return error is null ? ValidationResult.Success : new ValidationResult(error);
    }

    private void ValidateName()
    {
        if (IsEditingName)
        {
            ValidateProperty(EditName, nameof(EditName));
        }
        else
        {
            ClearErrors(nameof(EditName));
        }
    }

    public void OpenResultMenu()
    {
        var result = Result;
        var hidden = result.Count > 0 && result.All(IsHidden);
        _menu.Open(new MenuTarget([], result, () => result, IsSyncing, hidden, _token));
    }

    void IFilterEditor.OnEdited()
    {
        if (_loading || IsReadOnly)
        {
            return;
        }

        IsDirty = true;
        OnPropertyChanged(nameof(CategorySummary));
        _liveDebouncer.Trigger();
    }

    async Task IFilterEditor.LoadValuesAsync(RuleViewModel rule)
    {
        if (rule.Parameter is not { } parameter)
        {
            return;
        }

        var groups = CheckedGroups();
        var values = new Dictionary<string, int>(StringComparer.Ordinal);
        var hasMore = false;
        var missing = 0;
        foreach (var source in _data.Documents.Select(document => document.Snapshot.Source).Distinct())
        {
            var index = await _runtime.Values.GetAsync(source, parameter, groups, _token);
            hasMore |= index.HasMore;
            missing += index.NoParameterCount;
            foreach (var value in index.Values)
            {
                values[value.Value] = values.TryGetValue(value.Value, out var count) ? count + value.Count : value.Count;
            }
        }

        if (rule.Kind == ValueKind.YesNo)
        {
            rule.SetValues([
                new ValueCount(ValueKinds.Yes, values.TryGetValue(ValueKinds.Yes, out var yes) ? yes : 0),
                new ValueCount(ValueKinds.No, values.TryGetValue(ValueKinds.No, out var no) ? no : 0)
            ], false, missing);
            return;
        }

        rule.SetValues(values
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.CurrentCultureIgnoreCase)
            .Take(ValueIndexCache.MaxValues)
            .Select(pair => new ValueCount(pair.Key, pair.Value))
            .ToList(), hasMore || values.Count > ValueIndexCache.MaxValues, missing);
    }

    public FilterDefinition Definition()
    {
        return _current?.Kind == FilterSourceKind.RuleBased && _hostDefinition is not null
            ? _hostDefinition
            : new FilterDefinition(CheckedGroups(), Root.ToSet());
    }

    internal async Task LoadAsync(ContextData data, CancellationToken cancellationToken)
    {
        _data = data;
        _token = cancellationToken;
        RebuildCategories();
        await LoadParametersAsync(cancellationToken);
        await LoadSourcesAsync(cancellationToken);
    }

    internal async Task RefreshParametersAsync(SourceId source)
    {
        if (_data.Documents.Any(document => document.Snapshot.Source == source))
        {
            await LoadParametersAsync(_token);
        }
    }

    ParameterInfo? IFilterEditor.FindParameter(ParameterKey key)
    {
        return Parameters.FirstOrDefault(parameter => parameter.Key == key)
               ?? _allParameters.FirstOrDefault(parameter => parameter.Key == key);
    }

    internal FilterState Capture()
    {
        return _restore is { } pending ? pending with { IsLiveOn = _isLiveOn } : new FilterState(_current, _isLiveOn);
    }

    internal void Reset(FilterState state)
    {
        _restore = state;
        _evaluation?.Cancel();
        _liveDebouncer.Cancel();
        _data = ContextData.Empty;
        SetPending(null);
        IsLivePaused = false;
        if (_isLiveOn != state.IsLiveOn)
        {
            _isLiveOn = state.IsLiveOn;
            OnPropertyChanged(nameof(IsLiveOn));
            OnPropertyChanged(nameof(IsSyncing));
        }

        Result = [];
    }

    private async Task LoadParametersAsync(CancellationToken cancellationToken)
    {
        var catalogs = new List<(SourceId Source, IReadOnlyList<ParameterInfo> Catalog)>();
        foreach (var source in _data.Documents.Select(document => document.Snapshot.Source).Distinct())
        {
            catalogs.Add((source, await _runtime.ParametersAsync(source, cancellationToken)));
        }

        _catalogs = catalogs;
        _allParameters = ParameterCatalog.Merge(catalogs.Select(item => item.Catalog).ToList());
        UpdateParameters();
    }

    private void UpdateParameters()
    {
        var groups = CheckedGroups().ToHashSet();
        if (groups.Count == 0)
        {
            Parameters = _allParameters;
            return;
        }

        Parameters = ParameterCatalog.Merge(_catalogs
            .Where(item => HasAnyGroup(item.Source, groups))
            .Select(item => (IReadOnlyList<ParameterInfo>)item.Catalog
                .Where(parameter => parameter.Categories.Overlaps(groups))
                .ToList())
            .ToList());
    }

    private bool HasAnyGroup(SourceId source, HashSet<long> groups)
    {
        return _data.Documents
            .Where(document => document.Snapshot.Source == source)
            .Any(document => document.Snapshot.Groups.Any(group => groups.Contains(group.GroupId)));
    }

    internal async Task ActivateAsync()
    {
        if (IsLiveOn)
        {
            await CaptureBaselineAsync();
        }
    }

    private IReadOnlyList<long> CheckedGroups()
    {
        return Categories.Where(item => item.IsChecked).Select(item => item.GroupId).ToList();
    }

    private IEnumerable<string> OtherNames()
    {
        return Sources.Where(item => item.IsCustom && !ReferenceEquals(item, _current)).Select(item => item.Name);
    }

    private bool IsHidden(ElementRef element)
    {
        return _data.Find(element.Document) is { } scope
               && scope.Snapshot.TryGetRow(element.Id, out var row)
               && _visibility.IsHidden(element, scope.Snapshot.Groups[row.GroupIndex].GroupId);
    }

    private void SetPending(FilterSourceItem? item)
    {
        _pending = item;
        IsSwitchPending = item is not null;
    }

    private void RebuildCategories()
    {
        var checkedIds = CheckedGroups().ToHashSet();
        Categories.Clear();
        foreach (var group in _data.MergedGroups())
        {
            var item = new CategoryItemViewModel(group.GroupId, group.Name, group.Count, OnCategoryChanged);
            item.SetChecked(checkedIds.Contains(group.GroupId));
            Categories.Add(item);
        }

        UpdateVisibleCategories();
        OnPropertyChanged(nameof(CategorySummary));
    }

    private void OnCategoryChanged()
    {
        UpdateParameters();
        ((IFilterEditor)this).OnEdited();
    }

    [RelayCommand]
    private void CancelSwitch()
    {
        SetPending(null);
    }

    [RelayCommand]
    private void CheckAll()
    {
        CheckVisible(true);
    }

    [RelayCommand]
    private void CheckNone()
    {
        CheckVisible(false);
    }

    private void CheckVisible(bool value)
    {
        if (IsReadOnly)
        {
            return;
        }

        var changed = false;
        foreach (var item in VisibleCategories.Where(item => item.IsChecked != value))
        {
            item.SetChecked(value);
            changed = true;
        }

        if (changed)
        {
            UpdateParameters();
            ((IFilterEditor)this).OnEdited();
        }
    }

    private void UpdateVisibleCategories()
    {
        var text = CategorySearch.Trim();
        VisibleCategories = text.Length == 0
            ? Categories.ToList()
            : Categories.Where(item => item.Name.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
    }

    private async Task LoadSourcesAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<CustomFilter> saved = [];
        _runtime.TryRun(() => saved = _runtime.Services.CustomFilters.Load());
        var hosts = new List<FilterSourceItem>();
        foreach (var document in _data.Documents)
        {
            var filters = await _runtime.Services.HostFilters.GetFiltersAsync(document.Key, cancellationToken);
            hosts.AddRange(filters.Select(filter => FilterSourceItem.ForHost(filter, document.Label)));
        }

        var restore = _restore;
        var target = restore is null ? _current : restore.Selected;
        var items = saved.Select(filter => FilterSourceItem.Custom(filter, filter.Name)).ToList();
        if (target is { IsCustom: true, Saved: null } draft)
        {
            items.Add(draft);
        }

        items.AddRange(hosts);
        var keep = target is null ? null : items.FirstOrDefault(item => item.SameAs(target));
        EditSources(() =>
        {
            Sources.Clear();
            foreach (var item in items)
            {
                Sources.Add(item);
            }
        });

        if (restore is not null)
        {
            _restore = null;
            _current = null;
            await SwitchToAsync(keep);
        }
        else if (keep is null)
        {
            await SwitchToAsync(null);
        }
        else
        {
            if (!ReferenceEquals(keep, _current))
            {
                keep.Name = _current!.Name;
                keep.IsDirty = IsDirty;
                _current = keep;
            }

            OnPropertyChanged(nameof(SelectedSource));
            await EvaluateAsync(false);
        }
    }

    private void EditSources(Action edit)
    {
        _editingSources = true;
        try
        {
            edit();
        }
        finally
        {
            _editingSources = false;
        }
    }

    private async Task SwitchToAsync(FilterSourceItem? item)
    {
        SetPending(null);
        if (_current is { IsCustom: true, Saved: null } draft && !ReferenceEquals(draft, item))
        {
            EditSources(() => Sources.Remove(draft));
        }

        if (_current is not null)
        {
            _current.IsDirty = false;
            if (_current.Saved is { } saved)
            {
                _current.Name = saved.Name;
            }
        }

        if (item is not null && !Sources.Contains(item))
        {
            EditSources(() => Sources.Insert(Sources.Count(source => source.IsCustom), item));
        }

        _current = item;
        OnPropertyChanged(nameof(SelectedSource));
        RenameCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
        DuplicateCommand.NotifyCanExecuteChanged();
        await LoadEditorAsync(item);
        SaveCommand.NotifyCanExecuteChanged();
        _liveDebouncer.Trigger();
    }

    private async Task LoadEditorAsync(FilterSourceItem? item)
    {
        _loading = true;
        try
        {
            _hostDefinition = null;
            var definition = FilterDefinition.Empty;
            switch (item?.Kind)
            {
                case FilterSourceKind.Custom:
                    definition = item.Saved?.Definition ?? FilterDefinition.Empty;
                    break;
                case FilterSourceKind.RuleBased:
                    _hostDefinition = await _runtime.Services.HostFilters.ReadAsync(item.Host!.Document, item.Host.Id,
                        _token) ?? FilterDefinition.Empty;
                    definition = _hostDefinition;
                    break;
            }

            IsReadOnly = item is null || !item.IsCustom;
            var groups = definition.Groups.ToHashSet();
            foreach (var category in Categories)
            {
                category.SetChecked(groups.Contains(category.GroupId));
            }

            UpdateParameters();
            OnPropertyChanged(nameof(CategorySummary));
            Root = RuleSetViewModel.From(definition.Root, this, IsReadOnly);
            Name = item?.Name ?? string.Empty;
            IsEditingName = false;
            EditName = Name;
            ClearErrors(nameof(EditName));
            IsDirty = item is { IsCustom: true, Saved: null };
        }
        finally
        {
            _loading = false;
        }
    }

    partial void OnIsDirtyChanged(bool value)
    {
        if (_current is not null)
        {
            _current.IsDirty = value;
        }
    }

    partial void OnParametersChanged(IReadOnlyList<ParameterInfo> value)
    {
        Root.NotifyParameters();
    }

    partial void OnModeChanged(ResultSelectionMode value)
    {
        _liveDebouncer.Trigger();
    }

    private bool IsCustomSelected => _current is { IsCustom: true };

    private bool CanDuplicate => _current is { Kind: FilterSourceKind.RuleBased };

    [RelayCommand]
    private void Add()
    {
        var item = FilterSourceItem.Custom(null, FilterNames.NextFree(Sources.Where(source => source.IsCustom)
            .Select(source => source.Name)));
        if (IsDirty && _current is not null)
        {
            SetPending(item);
            return;
        }

        _ = _runtime.RunAsync(() => SwitchToNewAsync(item));
    }

    private async Task SwitchToNewAsync(FilterSourceItem item)
    {
        await SwitchToAsync(item);
        BeginRename();
    }

    [RelayCommand(CanExecute = nameof(IsCustomSelected))]
    private void Rename()
    {
        BeginRename();
    }

    [RelayCommand(CanExecute = nameof(IsCustomSelected))]
    private void Delete()
    {
        if (_current is not { IsCustom: true } item)
        {
            return;
        }

        var store = _runtime.Services.CustomFilters;
        if (item.Saved is { } saved && !_runtime.TryRun(() => store.Save(store.Load()
                .Where(filter => !string.Equals(filter.Name, saved.Name, StringComparison.OrdinalIgnoreCase))
                .ToList())))
        {
            return;
        }

        IsDirty = false;
        EditSources(() => Sources.Remove(item));
        _current = null;
        _ = _runtime.RunAsync(() => SwitchToAsync(Sources.FirstOrDefault(source => source.IsCustom)));
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        TrySave();
    }

    private bool TrySave()
    {
        if (_current is not { IsCustom: true } item || !CommitRename() || NameError is not null)
        {
            return false;
        }

        var filter = new CustomFilter(Name, Definition());
        var store = _runtime.Services.CustomFilters;
        var previous = item.Saved;
        if (!_runtime.TryRun(() => store.Save(Upsert(store.Load(), previous, filter))))
        {
            return false;
        }

        item.Saved = filter;
        item.Name = Name;
        IsDirty = false;
        return true;
    }

    private static List<CustomFilter> Upsert(IReadOnlyList<CustomFilter> stored, CustomFilter? previous, CustomFilter filter)
    {
        var filters = stored.ToList();
        var index = previous is null
            ? -1
            : filters.FindIndex(other => string.Equals(other.Name, previous.Name, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            filters[index] = filter;
        }
        else
        {
            filters.Add(filter);
        }

        return filters;
    }

    [RelayCommand(CanExecute = nameof(CanDuplicate))]
    private void Duplicate()
    {
        if (_current is not { Kind: FilterSourceKind.RuleBased } host || _hostDefinition is null)
        {
            return;
        }

        var definition = new FilterDefinition(_hostDefinition.Groups,
            Strip(_hostDefinition.Root));
        var name = FilterNames.Unique(host.Name, Sources.Where(source => source.IsCustom).Select(source => source.Name));
        var item = FilterSourceItem.Custom(null, name);
        _ = _runtime.RunAsync(async () =>
        {
            await SwitchToAsync(item);
            _loading = true;
            try
            {
                var groups = definition.Groups.ToHashSet();
                foreach (var category in Categories)
                {
                    category.SetChecked(groups.Contains(category.GroupId));
                }

                UpdateParameters();
                Root = RuleSetViewModel.From(definition.Root, this, false);
            }
            finally
            {
                _loading = false;
            }

            OnPropertyChanged(nameof(CategorySummary));
            _liveDebouncer.Trigger();
        });
    }

    private static FilterSet Strip(FilterSet set)
    {
        return set with
        {
            Children = set.Children
                .Where(child => child is not UnsupportedRule)
                .Select(child => child is FilterSet nested ? Strip(nested) : child)
                .ToList()
        };
    }

    [RelayCommand]
    private void SaveAndSwitch()
    {
        var target = _pending;
        if (!TrySave())
        {
            return;
        }

        _ = _runtime.RunAsync(() => SwitchPendingAsync(target));
    }

    [RelayCommand]
    private void DiscardAndSwitch()
    {
        var target = _pending;
        IsDirty = false;
        _ = _runtime.RunAsync(() => SwitchPendingAsync(target));
    }

    private async Task SwitchPendingAsync(FilterSourceItem? target)
    {
        var isNew = target is { IsCustom: true, Saved: null } && !Sources.Contains(target);
        await SwitchToAsync(target);
        if (isNew)
        {
            BeginRename();
        }
    }

    private async Task CaptureBaselineAsync()
    {
        var selection = await _runtime.Services.Selection.GetSelectionAsync(_token);
        _baseline = [.. selection];
    }

    private async Task EvaluateAsync(bool sync)
    {
        _evaluation?.Cancel();
        _evaluation?.Dispose();
        _evaluation = CancellationTokenSource.CreateLinkedTokenSource(_token);
        var token = _evaluation.Token;
        var timer = TraceTimer.Start(_runtime.Logger);
        var result = await ResultAsync(token);
        token.ThrowIfCancellationRequested();
        if (timer.IsEnabled)
        {
            _runtime.Logger.FilterEvaluated(_current?.Name ?? "(none)", result.Count, timer.ElapsedMs);
        }

        Result = result;
        if (!sync || !IsLiveOn || _current is null || (_current.IsCustom && IsEmpty(Definition())))
        {
            return;
        }

        if (result.Count > LivePauseLimit)
        {
            IsLivePaused = true;
            return;
        }

        IsLivePaused = false;
        IReadOnlyCollection<ElementRef> selection = Mode switch
        {
            ResultSelectionMode.Add => _baseline.Union(result).ToList(),
            ResultSelectionMode.Remove => _baseline.Except(result).ToList(),
            _ => result
        };
        await _runtime.SelectAsync(selection, token);
    }

    private static bool IsEmpty(FilterDefinition definition)
    {
        return definition.Groups.Count == 0 && definition.Root.Children.Count == 0;
    }

    private async Task<IReadOnlyList<ElementRef>> ResultAsync(CancellationToken cancellationToken)
    {
        var item = _current;
        if (item is null)
        {
            return [];
        }

        if (!item.IsCustom)
        {
            if (item.Host is not { } host || _data.Find(host.Document) is not { } scope)
            {
                return [];
            }

            var matches = await _runtime.Services.HostFilters.EvaluateAsync(scope.Key, host.Id, scope.Ids,
                cancellationToken);
            return matches.Where(scope.InScope).Select(id => new ElementRef(scope.Key, id)).ToList();
        }

        var definition = Definition();
        if (IsEmpty(definition))
        {
            return [];
        }

        var result = new List<ElementRef>();
        foreach (var document in _data.Documents)
        {
            var ids = await _runtime.Services.Evaluator.EvaluateAsync(document.Key, definition, document.Ids,
                cancellationToken);
            result.AddRange(ids.Select(id => new ElementRef(document.Key, id)));
        }

        return result;
    }
}
