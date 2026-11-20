using CommunityToolkit.Mvvm.ComponentModel;
using DevTools.ModelsContext.Diagnostics;
using DevTools.ModelsContext.Engine;
using DevTools.ModelsContext.Model;
using System.Globalization;

namespace DevTools.ModelsContext.ViewModels;

public sealed partial class FilterTabViewModel : ObservableObject, IFilterEditor, IFilterLibraryHost, IDisposable
{
    private readonly Dictionary<string, string?> _examples = new(StringComparer.Ordinal);
    private readonly ContextMenuViewModel _menu;
    private readonly PanelRuntime _runtime;
    private readonly ParameterScopes _scopes = new();
    private readonly VisibilityController _visibility;
    private ContextData _data = ContextData.Empty;
    private FilterDefinition? _hostDefinition;
    private bool _loading;
    private CancellationToken _token;

    internal FilterTabViewModel(PanelRuntime runtime, VisibilityController visibility, ContextMenuViewModel menu)
    {
        _runtime = runtime;
        _visibility = visibility;
        _menu = menu;
        Categories = new CategoryPickerViewModel(() => IsReadOnly, OnCategoriesChanged);
        Runner = new FilterRunner(runtime, Job);
        Library = new FilterLibraryViewModel(runtime, Runner, this);
        Root = new RuleSetViewModel(this, true);
    }

    public FilterLibraryViewModel Library { get; }

    public CategoryPickerViewModel Categories { get; }

    public FilterRunner Runner { get; }

    [ObservableProperty]
    public partial bool IsReadOnly { get; private set; }

    [ObservableProperty]
    public partial RuleSetViewModel Root { get; private set; }

    public EffectiveScope RootScope
    {
        get
        {
            var groups = Categories.Checked();
            return new EffectiveScope(_data.Documents.Select(document => document.Info.Source).Distinct().ToList(),
                groups.Count == 0 ? null : groups);
        }
    }

    ParameterScopes IFilterEditor.Parameters => _scopes;

    IReadOnlyList<long> IFilterEditor.AllCategories => Categories.Ids;

    FilterDefinition? IFilterLibraryHost.HostDefinition => _hostDefinition;

    public void Dispose()
    {
        Runner.Dispose();
    }

    public FilterDefinition Definition()
    {
        return Library.Current?.Kind == FilterSourceKind.RuleBased && _hostDefinition is not null
            ? _hostDefinition
            : new FilterDefinition(Categories.Checked(), Root.ToSet());
    }

    public void OpenResultMenu()
    {
        var result = Runner.Result;
        var hidden = result.Count > 0 && result.All(IsHidden);
        _menu.Open(new MenuTarget([], result, () => result, Runner.IsSyncing, hidden, _token));
    }

    public bool MoveTo(FilterNodeViewModel item, RuleSetViewModel target, int index)
    {
        return !IsReadOnly && item.MoveTo(target, index);
    }

    public string DocumentLabel(SourceId source)
    {
        if (_data.Known.FirstOrDefault(info => info.Source == source) is { } info)
        {
            return info.Key.IsCurrentModel ? ContextBarViewModel.CurrentModelLabel : info.DisplayName;
        }

        var name = Path.GetFileName(source.Value);
        return string.IsNullOrEmpty(name) ? source.Value : name;
    }

    void IFilterEditor.OnEdited()
    {
        if (_loading || IsReadOnly)
        {
            return;
        }

        Library.MarkDirty();
        Runner.Trigger();
    }

    bool IFilterEditor.IsKnown(SourceId source)
    {
        return _data.KnownSources.Contains(source);
    }

    string IFilterEditor.CategoryLabel(long categoryId)
    {
        return Categories.Label(categoryId) ?? categoryId.ToString(CultureInfo.InvariantCulture);
    }

    async Task IFilterEditor.LoadValuesAsync(RuleViewModel rule)
    {
        if (rule.Parameter is not { } parameter)
        {
            return;
        }

        var scope = rule.Group?.Effective ?? RootScope;
        if (scope.IsEmpty)
        {
            rule.SetValues([], false, 0);
            return;
        }

        var tally = await TallyValuesAsync(scope, parameter);
        if (rule.Kind == ValueKind.YesNo)
        {
            rule.SetValues(tally.YesNo(), false, tally.NoParameterCount);
            return;
        }

        rule.SetValues(tally.Top(ValueIndexCache.MaxValues), tally.IsTruncated(ValueIndexCache.MaxValues),
            tally.NoParameterCount);
    }

    private async Task<ValueTally> TallyValuesAsync(EffectiveScope scope, ParameterKey parameter)
    {
        IReadOnlyList<long> groups = scope.Categories?.ToList() ?? [];
        var tally = new ValueTally();
        foreach (var source in scope.Documents)
        {
            tally.Add(await _runtime.Values.GetAsync(source, parameter, groups, _token));
        }

        return tally;
    }

    async Task IFilterEditor.LoadExampleAsync(RuleViewModel rule)
    {
        if (rule.SelectedParameter is not { IsMeasured: true, Spec: { } spec } || _data.CurrentModel is not { } model)
        {
            rule.SetExample(null);
            return;
        }

        if (!_examples.TryGetValue(spec, out var example))
        {
            await _runtime.RunAsync(async () =>
                _examples[spec] = await _runtime.Services.Evaluator.FormatExampleAsync(model, spec, _token));
            _examples.TryGetValue(spec, out example);
        }

        if (rule.SelectedParameter?.Spec == spec)
        {
            rule.SetExample(example);
        }
    }

    async Task IFilterLibraryHost.LoadEditorAsync(FilterSourceItem? item)
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
            Show(definition, IsReadOnly);
        }
        finally
        {
            _loading = false;
        }
    }

    void IFilterLibraryHost.LoadDraft(FilterDefinition definition)
    {
        _loading = true;
        try
        {
            Show(definition, false);
        }
        finally
        {
            _loading = false;
        }
    }

    internal async Task LoadAsync(ContextData data, CancellationToken cancellationToken)
    {
        _data = data;
        _token = cancellationToken;
        _examples.Clear();
        Runner.Load(data, cancellationToken);
        Categories.Rebuild(data.MergedGroups());
        await LoadParametersAsync(cancellationToken);
        await Library.LoadAsync(data, cancellationToken);
    }

    internal async Task RefreshParametersAsync(SourceId source)
    {
        if (_data.Documents.Any(document => document.Snapshot.Source == source))
        {
            await LoadParametersAsync(_token);
        }
    }

    internal Task ActivateAsync()
    {
        return Runner.ActivateAsync();
    }

    internal FilterState Capture()
    {
        return Library.Capture(Runner.IsLiveOn);
    }

    internal void Reset(FilterState state)
    {
        Library.Reset(state);
        Runner.Reset(state.IsLiveOn);
        _data = ContextData.Empty;
    }

    partial void OnRootChanged(RuleSetViewModel value)
    {
        RefreshScope(value);
    }

    private FilterJob? Job()
    {
        return Library.Current is not { } item
            ? null
            : new FilterJob(item.Name, item.IsCustom ? null : item.Host, Definition());
    }

    private void Show(FilterDefinition definition, bool isReadOnly)
    {
        Categories.Check(definition.Groups);
        Root = RuleSetViewModel.From(definition.Root, this, isReadOnly);
    }

    private async Task LoadParametersAsync(CancellationToken cancellationToken)
    {
        var catalogs = new List<SourceCatalog>();
        foreach (var source in _data.Documents.Select(document => document.Snapshot.Source).Distinct())
        {
            var groups = _data.Documents
                .Where(document => document.Snapshot.Source == source)
                .SelectMany(document => document.Snapshot.Groups.Select(group => group.GroupId))
                .ToHashSet();
            catalogs.Add(new SourceCatalog(source, await _runtime.ParametersAsync(source, cancellationToken), groups));
        }

        _scopes.Set(catalogs, DocumentLabel, Categories.Label);
        RefreshScope(Root);
    }

    private void OnCategoriesChanged()
    {
        RefreshScope(Root);
        ((IFilterEditor)this).OnEdited();
    }

    private void RefreshScope(RuleSetViewModel root)
    {
        var timer = TraceTimer.Start(_runtime.Logger);
        root.RefreshScope();
        if (timer.IsEnabled)
        {
            _runtime.Logger.ParametersMerged(_scopes.CatalogCount, root.Parameters.Count, _scopes.ScopeCount,
                timer.ElapsedMs);
        }
    }

    private bool IsHidden(ElementRef element)
    {
        return _data.Find(element.Document) is { } scope
               && scope.Snapshot.TryGetRow(element.Id, out var row)
               && _visibility.IsHidden(element, scope.Snapshot.Groups[row.GroupIndex].GroupId);
    }
}
