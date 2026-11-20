using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTools.ModelsContext.Model;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;

namespace DevTools.ModelsContext.ViewModels;

internal interface IFilterLibraryHost
{
    FilterDefinition? HostDefinition { get; }

    FilterDefinition Definition();

    Task LoadEditorAsync(FilterSourceItem? item);

    void LoadDraft(FilterDefinition definition);
}

public sealed partial class FilterLibraryViewModel : ObservableValidator
{
    private readonly IFilterLibraryHost _host;
    private readonly FilterRunner _runner;
    private readonly PanelRuntime _runtime;
    private bool _editingSources;
    private FilterSourceItem? _pending;
    private FilterState? _restore;

    internal FilterLibraryViewModel(PanelRuntime runtime, FilterRunner runner, IFilterLibraryHost host)
    {
        _runtime = runtime;
        _runner = runner;
        _host = host;
        ErrorsChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(NameError));
            SaveCommand.NotifyCanExecuteChanged();
        };
    }

    public ObservableCollection<FilterSourceItem> Sources { get; } = [];

    public FilterSourceItem? SelectedSource
    {
        get => Current;
        set
        {
            if (_editingSources || ReferenceEquals(value, Current))
            {
                return;
            }

            if (IsDirty && Current is not null)
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
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial bool IsDirty { get; private set; }

    [ObservableProperty]
    public partial string Name { get; private set; } = string.Empty;

    [CustomValidation(typeof(FilterLibraryViewModel), nameof(CheckName))]
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

    public bool CanSave => Current is { IsCustom: true } && IsDirty && NameError is null;

    internal FilterSourceItem? Current { get; private set; }

    private bool IsCustomSelected => Current is { IsCustom: true };

    private bool CanDuplicate => Current is { Kind: FilterSourceKind.RuleBased };

    public static ValidationResult? CheckName(string name, ValidationContext context)
    {
        var error = FilterNames.Validate(name, ((FilterLibraryViewModel)context.ObjectInstance).OtherNames());
        return error is null ? ValidationResult.Success : new ValidationResult(error);
    }

    public void BeginRename()
    {
        if (Current is not { IsCustom: true })
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
            if (Current is { IsCustom: true } item)
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

    internal void MarkDirty()
    {
        IsDirty = true;
    }

    internal FilterState Capture(bool isLiveOn)
    {
        if (_restore is { } pending)
        {
            return pending with { IsLiveOn = isLiveOn };
        }

        var draft = IsDirty && Current is { IsCustom: true } ? new FilterDraft(_host.Definition(), Name) : null;
        return new FilterState(Current, isLiveOn, draft);
    }

    internal void Reset(FilterState state)
    {
        _restore = state;
        SetPending(null);
    }

    internal async Task LoadAsync(ContextData data, CancellationToken cancellationToken)
    {
        IReadOnlyList<CustomFilter> saved = [];
        _runtime.TryRun(() => saved = _runtime.Services.CustomFilters.Load());
        var hosts = await HostSourcesAsync(data, cancellationToken);
        var restore = _restore;
        var target = restore is null ? Current : restore.Selected;
        var items = saved.Select(filter => FilterSourceItem.Custom(filter, filter.Name)).ToList();
        if (target is { IsCustom: true, Saved: null } draft)
        {
            items.Add(draft);
        }

        items.AddRange(hosts);
        var keep = target is null ? null : items.FirstOrDefault(item => item.SameAs(target));
        EditSources(() => ReplaceSources(items));
        if (restore is not null)
        {
            _restore = null;
            Current = null;
            await SwitchToAsync(keep);
            if (keep is { IsCustom: true } && restore.Draft is { } edits)
            {
                Restore(keep, edits);
            }

            return;
        }

        await KeepAsync(keep);
    }

    private void Restore(FilterSourceItem item, FilterDraft draft)
    {
        _host.LoadDraft(draft.Definition);
        item.Name = draft.Name;
        Name = draft.Name;
        EditName = draft.Name;
        IsDirty = true;
        _runner.Trigger();
    }

    private async Task<List<FilterSourceItem>> HostSourcesAsync(ContextData data, CancellationToken cancellationToken)
    {
        var hosts = new List<FilterSourceItem>();
        foreach (var document in data.Documents)
        {
            var filters = await _runtime.Services.HostFilters.GetFiltersAsync(document.Key, cancellationToken);
            hosts.AddRange(filters.Select(filter => FilterSourceItem.ForHost(filter, document.Label)));
        }

        return hosts;
    }

    private void ReplaceSources(List<FilterSourceItem> items)
    {
        Sources.Clear();
        foreach (var item in items)
        {
            Sources.Add(item);
        }
    }

    private async Task KeepAsync(FilterSourceItem? keep)
    {
        if (keep is null)
        {
            await SwitchToAsync(null);
            return;
        }

        if (!ReferenceEquals(keep, Current))
        {
            keep.Name = Current!.Name;
            keep.IsDirty = IsDirty;
            Current = keep;
        }

        OnPropertyChanged(nameof(SelectedSource));
        await _runner.EvaluateAsync(false);
    }

    partial void OnIsDirtyChanged(bool value)
    {
        if (Current is not null)
        {
            Current.IsDirty = value;
        }
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

    private IEnumerable<string> OtherNames()
    {
        return Sources.Where(item => item.IsCustom && !ReferenceEquals(item, Current)).Select(item => item.Name);
    }

    private IEnumerable<string> CustomNames()
    {
        return Sources.Where(source => source.IsCustom).Select(source => source.Name);
    }

    private void SetPending(FilterSourceItem? item)
    {
        _pending = item;
        IsSwitchPending = item is not null;
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
        if (Current is { IsCustom: true, Saved: null } draft && !ReferenceEquals(draft, item))
        {
            EditSources(() => Sources.Remove(draft));
        }

        if (Current is not null)
        {
            Current.IsDirty = false;
            if (Current.Saved is { } saved)
            {
                Current.Name = saved.Name;
            }
        }

        if (item is not null && !Sources.Contains(item))
        {
            EditSources(() => Sources.Insert(Sources.Count(source => source.IsCustom), item));
        }

        Current = item;
        OnPropertyChanged(nameof(SelectedSource));
        RenameCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
        DuplicateCommand.NotifyCanExecuteChanged();
        await _host.LoadEditorAsync(item);
        Name = item?.Name ?? string.Empty;
        IsEditingName = false;
        EditName = Name;
        ClearErrors(nameof(EditName));
        IsDirty = item is { IsCustom: true, Saved: null };
        SaveCommand.NotifyCanExecuteChanged();
        _runner.Trigger();
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

    private bool TrySave()
    {
        if (Current is not { IsCustom: true } item || !CommitRename() || NameError is not null)
        {
            return false;
        }

        var filter = new CustomFilter(Name, _host.Definition());
        var store = _runtime.Services.CustomFilters;
        var previous = item.Saved;
        if (!_runtime.TryRun(() => store.Save(CustomFilterLibrary.Upsert(store.Load(), previous, filter))))
        {
            return false;
        }

        item.Saved = filter;
        item.Name = Name;
        IsDirty = false;
        return true;
    }

    [RelayCommand]
    private void Add()
    {
        var item = FilterSourceItem.Custom(null, FilterNames.NextFree(CustomNames()));
        if (IsDirty && Current is not null)
        {
            SetPending(item);
            return;
        }

        _ = _runtime.RunAsync(async () =>
        {
            await SwitchToAsync(item);
            BeginRename();
        });
    }

    [RelayCommand(CanExecute = nameof(IsCustomSelected))]
    private void Rename()
    {
        BeginRename();
    }

    [RelayCommand(CanExecute = nameof(IsCustomSelected))]
    private void Delete()
    {
        if (Current is not { IsCustom: true } item)
        {
            return;
        }

        var store = _runtime.Services.CustomFilters;
        if (item.Saved is { } saved && !_runtime.TryRun(() => store.Save(CustomFilterLibrary.Remove(store.Load(), saved.Name))))
        {
            return;
        }

        IsDirty = false;
        EditSources(() => Sources.Remove(item));
        Current = null;
        _ = _runtime.RunAsync(() => SwitchToAsync(Sources.FirstOrDefault(source => source.IsCustom)));
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        TrySave();
    }

    [RelayCommand(CanExecute = nameof(CanDuplicate))]
    private void Duplicate()
    {
        if (Current is not { Kind: FilterSourceKind.RuleBased } host || _host.HostDefinition is not { } source)
        {
            return;
        }

        var definition = CustomFilterLibrary.Strip(source);
        var item = FilterSourceItem.Custom(null, FilterNames.Unique(host.Name, CustomNames()));
        _ = _runtime.RunAsync(async () =>
        {
            await SwitchToAsync(item);
            _host.LoadDraft(definition);
            _runner.Trigger();
        });
    }

    [RelayCommand]
    private void SaveAndSwitch()
    {
        var target = _pending;
        if (TrySave())
        {
            _ = _runtime.RunAsync(() => SwitchPendingAsync(target));
        }
    }

    [RelayCommand]
    private void DiscardAndSwitch()
    {
        var target = _pending;
        IsDirty = false;
        _ = _runtime.RunAsync(() => SwitchPendingAsync(target));
    }

    [RelayCommand]
    private void CancelSwitch()
    {
        SetPending(null);
    }
}
