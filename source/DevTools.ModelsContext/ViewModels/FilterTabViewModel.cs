using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTools.ModelsContext.Engine;
using DevTools.ModelsContext.Model;
using System.Collections.ObjectModel;
using System.Globalization;

namespace DevTools.ModelsContext.ViewModels;

public sealed class FilterTabViewModel : ObservableObject, IFilterEditor, IDisposable
{
    public const int LivePauseLimit = 5000;
    public const string AllCategories = "All categories";

    public static readonly TimeSpan LiveDelay = TimeSpan.FromMilliseconds(300);

    private readonly Debouncer _liveDebouncer;
    private readonly ContextMenuViewModel _menu;
    private readonly PanelRuntime _runtime;
    private readonly VisibilityController _visibility;
    private HashSet<ElementRef> _baseline = [];
    private string _categorySearch = string.Empty;
    private FilterSourceItem? _current;
    private ContextData _data = ContextData.Empty;
    private string _editName = string.Empty;
    private CancellationTokenSource? _evaluation;
    private FilterDefinition? _hostDefinition;
    private IReadOnlyCollection<long> _hostSelectionIds = [];
    private bool _isDirty;
    private bool _isEditingName;
    private bool _isLiveOn = true;
    private bool _isLivePaused;
    private bool _isReadOnly;
    private bool _isSwitchPending;
    private bool _loading;
    private ResultSelectionMode _mode;
    private string _name = string.Empty;
    private string? _nameError;
    private IReadOnlyList<ParameterInfo> _parameters = [];
    private FilterSourceItem? _pending;
    private FilterState? _restore;
    private IReadOnlyList<ElementRef> _result = [];
    private RuleSetViewModel _root;
    private CancellationToken _token;
    private IReadOnlyList<CategoryItemViewModel> _visibleCategories = [];

    internal FilterTabViewModel(PanelRuntime runtime, VisibilityController visibility, ContextMenuViewModel menu)
    {
        _runtime = runtime;
        _visibility = visibility;
        _menu = menu;
        _root = new RuleSetViewModel(this, true);
        _liveDebouncer = new Debouncer(runtime.Services.Scheduler, LiveDelay,
            () => _ = _runtime.RunAsync(EvaluateAsync));
        AddCommand = new RelayCommand(AddFilter);
        DeleteCommand = new RelayCommand(Delete, () => _current is { IsCustom: true });
        SaveCommand = new RelayCommand(() => Save(), () => CanSave);
        DuplicateCommand = new RelayCommand(Duplicate, () => _current is { Kind: FilterSourceKind.RuleBased });
        SaveAndSwitchCommand = new RelayCommand(SaveAndSwitch);
        DiscardAndSwitchCommand = new RelayCommand(DiscardAndSwitch);
        CancelSwitchCommand = new RelayCommand(() => SetPending(null));
    }

    public ObservableCollection<FilterSourceItem> Sources { get; } = [];

    public FilterSourceItem? SelectedSource
    {
        get => _current;
        set
        {
            if (ReferenceEquals(value, _current))
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

    public bool IsSwitchPending
    {
        get => _isSwitchPending;
        private set => SetProperty(ref _isSwitchPending, value);
    }

    public IRelayCommand AddCommand { get; }

    public IRelayCommand DeleteCommand { get; }

    public IRelayCommand SaveCommand { get; }

    public IRelayCommand DuplicateCommand { get; }

    public IRelayCommand SaveAndSwitchCommand { get; }

    public IRelayCommand DiscardAndSwitchCommand { get; }

    public IRelayCommand CancelSwitchCommand { get; }

    public bool IsReadOnly
    {
        get => _isReadOnly;
        private set => SetProperty(ref _isReadOnly, value);
    }

    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            if (SetProperty(ref _isDirty, value))
            {
                if (_current is not null)
                {
                    _current.IsDirty = value;
                }

                SaveCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string Name
    {
        get => _name;
        private set => SetProperty(ref _name, value);
    }

    public string EditName
    {
        get => _editName;
        set
        {
            if (SetProperty(ref _editName, value ?? string.Empty))
            {
                NameError = IsEditingName ? FilterNames.Validate(_editName, OtherNames()) : null;
            }
        }
    }

    public string? NameError
    {
        get => _nameError;
        private set
        {
            if (SetProperty(ref _nameError, value))
            {
                SaveCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsEditingName
    {
        get => _isEditingName;
        private set => SetProperty(ref _isEditingName, value);
    }

    public bool CanSave => _current is { IsCustom: true } && IsDirty && NameError is null;

    public ObservableCollection<CategoryItemViewModel> Categories { get; } = [];

    public string CategorySearch
    {
        get => _categorySearch;
        set
        {
            if (SetProperty(ref _categorySearch, value ?? string.Empty))
            {
                UpdateVisibleCategories();
            }
        }
    }

    public IReadOnlyList<CategoryItemViewModel> VisibleCategories
    {
        get => _visibleCategories;
        private set => SetProperty(ref _visibleCategories, value);
    }

    public string CategorySummary
    {
        get
        {
            var names = Categories.Where(item => item.IsChecked).Select(item => item.Name).ToList();
            return names.Count == 0 ? AllCategories : string.Join(", ", names);
        }
    }

    public IReadOnlyList<ParameterInfo> Parameters
    {
        get => _parameters;
        private set
        {
            if (SetProperty(ref _parameters, value))
            {
                Root.NotifyParameters();
            }
        }
    }

    public RuleSetViewModel Root
    {
        get => _root;
        private set => SetProperty(ref _root, value);
    }

    public IReadOnlyList<ElementRef> Result
    {
        get => _result;
        private set
        {
            if (SetProperty(ref _result, value))
            {
                OnPropertyChanged(nameof(ResultCount));
                OnPropertyChanged(nameof(ResultText));
            }
        }
    }

    public int ResultCount => Result.Count;

    public string ResultText => string.Format(CultureInfo.CurrentCulture, "{0:N0} elements", ResultCount);

    public ResultSelectionMode Mode
    {
        get => _mode;
        set
        {
            if (SetProperty(ref _mode, value))
            {
                _liveDebouncer.Trigger();
            }
        }
    }

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

    public bool IsLivePaused
    {
        get => _isLivePaused;
        private set
        {
            if (SetProperty(ref _isLivePaused, value))
            {
                OnPropertyChanged(nameof(IsSyncing));
            }
        }
    }

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
        NameError = FilterNames.Validate(EditName, OtherNames());
    }

    public bool CommitRename()
    {
        if (!IsEditingName)
        {
            return true;
        }

        NameError = FilterNames.Validate(EditName, OtherNames());
        if (NameError is not null)
        {
            return false;
        }

        var trimmed = EditName.Trim();
        IsEditingName = false;
        _editName = trimmed;
        OnPropertyChanged(nameof(EditName));
        if (!string.Equals(trimmed, Name, StringComparison.Ordinal))
        {
            Name = trimmed;
            IsDirty = true;
        }

        return true;
    }

    public void CancelRename()
    {
        IsEditingName = false;
        _editName = Name;
        OnPropertyChanged(nameof(EditName));
        NameError = null;
    }

    public void LeaveRename()
    {
        if (!CommitRename())
        {
            CancelRename();
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
        Parameters = await LoadParametersAsync(cancellationToken);
        await LoadSourcesAsync(cancellationToken);
    }

    internal async Task RefreshParametersAsync(SourceId source)
    {
        if (_data.Documents.Any(document => document.Snapshot.Source == source))
        {
            Parameters = await LoadParametersAsync(_token);
        }
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

    private async Task<IReadOnlyList<ParameterInfo>> LoadParametersAsync(CancellationToken cancellationToken)
    {
        var merged = new Dictionary<ParameterKey, ParameterInfo>();
        foreach (var source in _data.Documents.Select(document => document.Snapshot.Source).Distinct())
        {
            foreach (var parameter in await _runtime.ParametersAsync(source, cancellationToken))
            {
                if (!merged.ContainsKey(parameter.Key))
                {
                    merged.Add(parameter.Key, parameter);
                }
            }
        }

        return merged.Values
            .OrderBy(parameter => parameter.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
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
        ((IFilterEditor)this).OnEdited();
    }

    private void UpdateVisibleCategories()
    {
        var text = _categorySearch.Trim();
        VisibleCategories = text.Length == 0
            ? Categories.ToList()
            : Categories.Where(item => item.Name.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
    }

    private async Task LoadSourcesAsync(CancellationToken cancellationToken)
    {
        var restore = _restore;
        var target = restore is null ? _current : restore.Selected;
        var items = _runtime.Services.CustomFilters.Load()
            .Select(filter => FilterSourceItem.Custom(filter, filter.Name))
            .ToList();
        if (target is { IsCustom: true, Saved: null } draft)
        {
            items.Add(draft);
        }

        foreach (var document in _data.Documents)
        {
            var filters = await _runtime.Services.HostFilters.GetFiltersAsync(document.Key, cancellationToken);
            items.AddRange(filters.Select(filter => FilterSourceItem.ForHost(filter, document.Label)));
        }

        var keep = target is null ? null : items.FirstOrDefault(item => item.SameAs(target));
        Sources.Clear();
        foreach (var item in items)
        {
            Sources.Add(item);
        }

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
        else if (!ReferenceEquals(keep, _current))
        {
            keep.Name = _current!.Name;
            keep.IsDirty = IsDirty;
            _current = keep;
            OnPropertyChanged(nameof(SelectedSource));
        }
    }

    private async Task SwitchToAsync(FilterSourceItem? item)
    {
        SetPending(null);
        if (_current is { IsCustom: true, Saved: null } draft && !ReferenceEquals(draft, item))
        {
            Sources.Remove(draft);
        }

        if (_current is not null)
        {
            _current.IsDirty = false;
        }

        if (item is not null && !Sources.Contains(item))
        {
            Sources.Insert(Sources.Count(source => source.IsCustom), item);
        }

        _current = item;
        OnPropertyChanged(nameof(SelectedSource));
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
            _hostSelectionIds = [];
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
                case FilterSourceKind.Selection:
                    _hostSelectionIds = await _runtime.Services.HostFilters.GetSelectionIdsAsync(item.Host!.Document,
                        item.Host.Id, _token);
                    break;
            }

            IsReadOnly = item is null || !item.IsCustom;
            var groups = definition.Groups.ToHashSet();
            foreach (var category in Categories)
            {
                category.SetChecked(groups.Contains(category.GroupId));
            }

            OnPropertyChanged(nameof(CategorySummary));
            Root = RuleSetViewModel.From(definition.Root, this, IsReadOnly);
            Name = item?.Name ?? string.Empty;
            IsEditingName = false;
            _editName = Name;
            OnPropertyChanged(nameof(EditName));
            NameError = null;
            IsDirty = item is { IsCustom: true, Saved: null };
        }
        finally
        {
            _loading = false;
        }
    }

    private void AddFilter()
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

    private void Delete()
    {
        if (_current is not { IsCustom: true } item)
        {
            return;
        }

        if (item.Saved is { } saved)
        {
            _runtime.Services.CustomFilters.Save(_runtime.Services.CustomFilters.Load()
                .Where(filter => !string.Equals(filter.Name, saved.Name, StringComparison.OrdinalIgnoreCase))
                .ToList());
        }

        IsDirty = false;
        Sources.Remove(item);
        _current = null;
        _ = _runtime.RunAsync(() => SwitchToAsync(Sources.FirstOrDefault(source => source.IsCustom)));
    }

    private bool Save()
    {
        if (_current is not { IsCustom: true } item || !CommitRename() || NameError is not null)
        {
            return false;
        }

        var filter = new CustomFilter(Name, Definition());
        var store = _runtime.Services.CustomFilters;
        var filters = store.Load().ToList();
        var index = item.Saved is { } saved
            ? filters.FindIndex(other => string.Equals(other.Name, saved.Name, StringComparison.OrdinalIgnoreCase))
            : -1;
        if (index >= 0)
        {
            filters[index] = filter;
        }
        else
        {
            filters.Add(filter);
        }

        store.Save(filters);
        item.Saved = filter;
        item.Name = Name;
        IsDirty = false;
        return true;
    }

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
        return new FilterSet(set.Match, set.Children
            .Where(child => child is not UnsupportedRule)
            .Select(child => child is FilterSet nested ? Strip(nested) : child)
            .ToList());
    }

    private void SaveAndSwitch()
    {
        var target = _pending;
        if (!Save())
        {
            return;
        }

        _ = _runtime.RunAsync(() => SwitchPendingAsync(target));
    }

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

    private async Task EvaluateAsync()
    {
        _evaluation?.Cancel();
        _evaluation?.Dispose();
        _evaluation = CancellationTokenSource.CreateLinkedTokenSource(_token);
        var token = _evaluation.Token;
        var result = await ResultAsync(token);
        token.ThrowIfCancellationRequested();
        Result = result;
        if (!IsLiveOn || _current is null || (_current.IsCustom && IsEmpty(Definition())))
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

        if (item.Kind == FilterSourceKind.Selection)
        {
            return _data.Find(item.Document!.Value) is { } scope
                ? _hostSelectionIds.Where(scope.InScope).Select(id => new ElementRef(scope.Key, id)).ToList()
                : [];
        }

        var definition = Definition();
        if (item.IsCustom && IsEmpty(definition))
        {
            return [];
        }

        var documents = item.IsCustom
            ? _data.Documents
            : _data.Documents.Where(document => document.Key == item.Document).ToList();
        var result = new List<ElementRef>();
        foreach (var document in documents)
        {
            var ids = await _runtime.Services.Evaluator.EvaluateAsync(document.Key, definition, document.Ids,
                cancellationToken);
            result.AddRange(ids.Select(id => new ElementRef(document.Key, id)));
        }

        return result;
    }
}
