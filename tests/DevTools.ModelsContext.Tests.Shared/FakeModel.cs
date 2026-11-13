using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Ports;
using System.Globalization;

namespace DevTools.ModelsContext.Tests.Shared;

public sealed class FakeModel :
    IModelSource,
    IModelEvents,
    ISelectionPort,
    IViewActions,
    IFilterEvaluator,
    IHostFilterCatalog,
    ICustomFilterStore
{
    private readonly Dictionary<string, List<DocumentInfo>> _documents = new(StringComparer.Ordinal);
    private readonly Dictionary<SourceId, List<FakeVersion>> _history = [];
    private readonly string _primary;
    private readonly Dictionary<SourceId, DocumentSnapshot> _snapshots = [];
    private readonly Dictionary<SourceId, FakeSource> _sources = [];
    private string _active;
    private List<CustomFilter> _customFilters = [];
    private TaskCompletionSource<bool>? _scopeGate;
    private IReadOnlyCollection<ElementRef> _selection = [];
    private TaskCompletionSource<bool>? _snapshotGate;

    public FakeModel(FakeModelData data)
    {
        _primary = data.CurrentModel.ModelId;
        _active = _primary;
        Data = data;
        AddModel(data);
        View = new ViewInfo(data.CurrentModel, "view-1", "Level 1", false);
    }

    public FakeModel(FakeModelOptions? options = null)
        : this(FakeModelGenerator.Generate(options))
    {
    }

    public FakeModelData Data { get; private set; }

    public DocumentKey ActiveModel => DocumentKey.Current(_active);

    public ViewInfo View { get; set; }

    public int BuildCount { get; private set; }

    public int DocumentReads { get; private set; }

    public int SelectionReads { get; private set; }

    public bool EchoSelection { get; set; } = true;

    public bool ReverseRebuilds { get; set; }

    public bool IsBusy { get; private set; }

    public List<VisibilityPlan> ExecutedPlans { get; } = [];

    public int KeepCount { get; private set; }

    public int ResetCount { get; private set; }

    public List<IReadOnlyCollection<ElementRef>> Zoomed { get; } = [];

    public List<IReadOnlyCollection<ElementRef>> Boxed { get; } = [];

    public event EventHandler? DocumentsChanged;

    public event EventHandler<ElementDelta>? ElementsChanged;

    public event EventHandler<IReadOnlyCollection<ElementRef>>? SelectionChanged;

    public event EventHandler<ViewInfo>? ViewActivated;

    public event EventHandler<DocumentKey>? DocumentClosing;

    public event EventHandler<DocumentKey>? ActiveModelChanged;

    public event EventHandler<DocumentKey>? ModelSynced;

    public event EventHandler<LinkStatusChange>? LinkStatusChanged;

    public event EventHandler<bool>? BusyChanged;

    public TreeSchema Schema => FakeModelGenerator.Schema;

    public Task<IReadOnlyList<DocumentInfo>> GetDocumentsAsync(CancellationToken cancellationToken)
    {
        DocumentReads++;
        IReadOnlyList<DocumentInfo> documents = _documents.TryGetValue(_active, out var list) ? list.ToList() : [];
        return Task.FromResult(documents);
    }

    public Task<DocumentSnapshot> BuildSnapshotAsync(
        SourceId source,
        IProgress<SnapshotProgress>? progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _snapshotGate is { } gate
            ? BuildAfterAsync(gate.Task, source, progress)
            : Task.FromResult(Build(source, progress));
    }

    public Task<IReadOnlyCollection<long>> CollectScopeAsync(
        DocumentKey document,
        ScopeKind scope,
        CancellationToken cancellationToken)
    {
        var ids = ScopeIds(document, scope);
        return _scopeGate is { } gate ? AfterAsync(gate.Task, ids) : Task.FromResult(ids);
    }

    public Task<IReadOnlyList<InstanceName>> GetInstanceNamesAsync(
        DocumentKey document,
        IReadOnlyList<long> ids,
        CancellationToken cancellationToken)
    {
        var snapshot = SnapshotOf(document);
        IReadOnlyList<InstanceName> names = ids
            .Select(id => snapshot.TryGetRow(id, out var row)
                ? new InstanceName(id, $"{snapshot.TypeName(row.TypeIndex)} [{id}]")
                : new InstanceName(id, $"[{id}]"))
            .ToList();
        return Task.FromResult(names);
    }

    public Task<IReadOnlyList<GroupCount>> CountGroupsAsync(
        DocumentKey document,
        ScopeKind scope,
        CancellationToken cancellationToken)
    {
        var source = SourceOf(document);
        var types = source.Types.ToDictionary(type => type.TypeId);
        var inScope = new HashSet<long>(ScopeIds(document, scope));
        IReadOnlyList<GroupCount> counts = source.Elements
            .Where(element => inScope.Contains(element.Id))
            .GroupBy(element => types[element.TypeId].GroupId)
            .Select(group => new GroupCount(group.Key, types[group.First().TypeId].GroupName, group.Count()))
            .ToList();
        return Task.FromResult(counts);
    }

    public Task<DocumentVersion?> GetVersionAsync(SourceId source, CancellationToken cancellationToken)
    {
        return Task.FromResult<DocumentVersion?>(VersionOf(source));
    }

    public Task<ElementDelta?> GetChangesSinceAsync(
        SourceId source,
        DocumentVersion since,
        CancellationToken cancellationToken)
    {
        var history = _history[source];
        var old = history.FirstOrDefault(version => version.Version.IsSameAs(since));
        return Task.FromResult(old is null ? null : Diff(source, old.Elements, _sources[source].Elements));
    }

    public Task<IReadOnlyList<ParameterInfo>> GetParameterCatalogAsync(
        SourceId source,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(FakeParameters.Catalog(_sources[source]));
    }

    public Task<IReadOnlyDictionary<long, string>> GetUniqueIdsAsync(
        DocumentKey document,
        IReadOnlyCollection<long> ids,
        CancellationToken cancellationToken)
    {
        var source = DocumentOf(document).Source;
        IReadOnlyDictionary<long, string> uniqueIds = ids.Distinct().ToDictionary(id => id,
            id => string.Format(CultureInfo.InvariantCulture, "{0}-{1:x8}", source.Value, id));
        return Task.FromResult(uniqueIds);
    }

    public Task<IReadOnlyCollection<ElementRef>> GetSelectionAsync(CancellationToken cancellationToken)
    {
        SelectionReads++;
        return Task.FromResult(_selection);
    }

    public Task SelectAsync(IReadOnlyCollection<ElementRef> elements, CancellationToken cancellationToken)
    {
        _selection = elements.ToList();
        if (EchoSelection)
        {
            SelectionChanged?.Invoke(this, _selection);
        }

        return Task.CompletedTask;
    }

    public Task ExecuteAsync(VisibilityPlan plan, CancellationToken cancellationToken)
    {
        if (plan.IsDisabled)
        {
            throw new InvalidOperationException(plan.DisabledReason);
        }

        ExecutedPlans.Add(plan);
        return Task.CompletedTask;
    }

    public Task KeepAsync(CancellationToken cancellationToken)
    {
        KeepCount++;
        return Task.CompletedTask;
    }

    public Task ResetAsync(CancellationToken cancellationToken)
    {
        ResetCount++;
        ExecutedPlans.Clear();
        return Task.CompletedTask;
    }

    public Task ZoomAsync(IReadOnlyCollection<ElementRef> elements, CancellationToken cancellationToken)
    {
        Zoomed.Add(elements);
        return Task.CompletedTask;
    }

    public Task SectionBoxAsync(IReadOnlyCollection<ElementRef> elements, CancellationToken cancellationToken)
    {
        if (!View.Is3D)
        {
            throw new InvalidOperationException("Section box needs a 3D view.");
        }

        Boxed.Add(elements);
        return Task.CompletedTask;
    }

    public Task<TemporaryState?> ReadTemporaryStateAsync(CancellationToken cancellationToken)
    {
        var active = ExecutedPlans.Count > 0;
        return Task.FromResult<TemporaryState?>(new TemporaryState(View, active, false));
    }

    public Task<IReadOnlyCollection<long>> GetHiddenIdsAsync(DocumentKey document, CancellationToken cancellationToken)
    {
        var snapshot = SnapshotOf(document);
        var hidden = new HashSet<long>();
        HashSet<long>? kept = null;
        foreach (var plan in ExecutedPlans)
        {
            if (plan.Action == VisibilityAction.Hide)
            {
                hidden.UnionWith(IdsOf(plan, document, snapshot));
            }
            else if (plan.Action == VisibilityAction.Isolate)
            {
                kept = [.. IdsOf(plan, document, snapshot)];
            }
        }

        if (kept is not null)
        {
            hidden.UnionWith(snapshot.Rows.Where(row => !kept.Contains(row.Id)).Select(row => row.Id));
        }

        return Task.FromResult<IReadOnlyCollection<long>>(hidden);
    }

    public Task<IReadOnlyCollection<long>> EvaluateAsync(
        DocumentKey document,
        FilterDefinition filter,
        IReadOnlyCollection<long>? scope,
        CancellationToken cancellationToken)
    {
        var snapshot = SnapshotOf(document);
        var groups = filter.Groups.ToHashSet();
        var inScope = scope as ISet<long> ?? scope?.ToHashSet();
        IReadOnlyCollection<long> ids = snapshot.Rows
            .Where(row => groups.Count == 0 || groups.Contains(snapshot.Groups[row.GroupIndex].GroupId))
            .Where(row => inScope is null || inScope.Contains(row.Id))
            .Where(row => FakeFilterMatcher.Matches(snapshot, row, filter.Root, Data.Levels))
            .Select(row => row.Id)
            .ToList();
        return Task.FromResult(ids);
    }

    public Task<ParameterValueCounts> CountValuesAsync(
        SourceId source,
        ParameterKey parameter,
        IReadOnlyCollection<long> groups,
        CancellationToken cancellationToken)
    {
        var snapshot = SnapshotOf(source);
        var wanted = groups.ToHashSet();
        var total = 0;
        var values = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var row in snapshot.Rows)
        {
            if (wanted.Count > 0 && !wanted.Contains(snapshot.Groups[row.GroupIndex].GroupId))
            {
                continue;
            }

            total++;
            if (FakeParameters.TryGet(snapshot, row, parameter, Data.Levels, out var value))
            {
                values[value] = values.TryGetValue(value, out var count) ? count + 1 : 1;
            }
        }

        return Task.FromResult(new ParameterValueCounts(total, values));
    }

    public Task<IReadOnlyList<HostFilterInfo>> GetFiltersAsync(
        DocumentKey document,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<HostFilterInfo> filters =
        [
            new(document, 1, "Commented", HostFilterKind.RuleBased, document.IsCurrentModel),
            new(document, 2, "Picked", HostFilterKind.Selection, false)
        ];
        return Task.FromResult(filters);
    }

    public Task<FilterDefinition?> ReadAsync(DocumentKey document, long filterId, CancellationToken cancellationToken)
    {
        FilterDefinition? definition = filterId == 1
            ? new FilterDefinition([],
                FilterSet.All(new FilterRule(FakeParameters.Comments, ValueKind.Text, FilterOperator.HasValue),
                    new UnsupportedRule("Global parameter association")))
            : null;
        return Task.FromResult(definition);
    }

    public Task<IReadOnlyCollection<long>> GetSelectionIdsAsync(
        DocumentKey document,
        long filterId,
        CancellationToken cancellationToken)
    {
        IReadOnlyCollection<long> ids = filterId == 2
            ? SourceOf(document).Elements.Take(10).Select(element => element.Id).ToList()
            : [];
        return Task.FromResult(ids);
    }

    public IReadOnlyList<CustomFilter> Load()
    {
        return _customFilters;
    }

    public void Save(IReadOnlyList<CustomFilter> filters)
    {
        _customFilters = filters.ToList();
    }

    public void AddModel(FakeModelData data)
    {
        _documents[data.CurrentModel.ModelId] = data.Documents.ToList();
        foreach (var pair in data.Sources)
        {
            if (_sources.ContainsKey(pair.Key))
            {
                continue;
            }

            _sources.Add(pair.Key, pair.Value);
            _history.Add(pair.Key, [new FakeVersion(new DocumentVersion($"{pair.Key.Value}-1", 1), pair.Value.Elements)]);
        }
    }

    public void ActivateModel(DocumentKey model)
    {
        _active = model.ModelId;
        View = new ViewInfo(model, "view-1", "Level 1", false);
        _selection = [];
        ActiveModelChanged?.Invoke(this, model);
    }

    public void CloseModel(DocumentKey model)
    {
        DocumentClosing?.Invoke(this, model);
        _documents.Remove(model.ModelId);
        if (_active == model.ModelId)
        {
            _active = _documents.Keys.FirstOrDefault() ?? string.Empty;
        }
    }

    public void SetBusy(bool busy)
    {
        IsBusy = busy;
        BusyChanged?.Invoke(this, busy);
    }

    public void SetLoaded(DocumentKey link, bool loaded)
    {
        var list = _documents[link.ModelId];
        var index = list.FindIndex(document => document.Key == link);
        list[index] = list[index] with { IsLoaded = loaded };
        RefreshData();
        LinkStatusChanged?.Invoke(this, new LinkStatusChange(link, loaded ? LinkStatus.Reloaded : LinkStatus.Unloaded));
    }

    public DocumentVersion VersionOf(SourceId source)
    {
        return _history[source][_history[source].Count - 1].Version;
    }

    public DocumentVersion SaveVersion(
        SourceId source,
        IReadOnlyList<ElementRecord>? added = null,
        IReadOnlyList<long>? deleted = null,
        IReadOnlyList<ElementRecord>? modified = null)
    {
        var current = _sources[source];
        var gone = new HashSet<long>(deleted ?? []);
        var changed = (modified ?? []).ToDictionary(element => element.Id);
        var elements = current.Elements
            .Where(element => !gone.Contains(element.Id))
            .Select(element => changed.TryGetValue(element.Id, out var replacement) ? replacement : element)
            .Concat(added ?? [])
            .ToList();
        _sources[source] = current with { Elements = elements };
        var history = _history[source];
        var previous = history[history.Count - 1].Version;
        var version = new DocumentVersion($"{source.Value}-{history.Count + 1}", previous.Saves + 1);
        history.Add(new FakeVersion(version, elements));
        RefreshData();
        return version;
    }

    public void ForgetHistory(SourceId source)
    {
        var history = _history[source];
        history.RemoveRange(0, history.Count - 1);
    }

    public void HoldScopes()
    {
        _scopeGate = new TaskCompletionSource<bool>();
    }

    public void ReleaseScopes()
    {
        var gate = _scopeGate;
        _scopeGate = null;
        gate?.TrySetResult(true);
    }

    public void HoldSnapshots()
    {
        _snapshotGate = new TaskCompletionSource<bool>();
    }

    public void ReleaseSnapshots()
    {
        var gate = _snapshotGate;
        _snapshotGate = null;
        gate?.TrySetResult(true);
    }

    public void RaiseDocumentsChanged()
    {
        DocumentsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RaiseElementsChanged(ElementDelta delta)
    {
        ElementsChanged?.Invoke(this, delta);
    }

    public void RaiseSelectionChanged(IReadOnlyCollection<ElementRef> selection)
    {
        _selection = selection;
        SelectionChanged?.Invoke(this, selection);
    }

    public void RaiseViewActivated(ViewInfo view)
    {
        View = view;
        ViewActivated?.Invoke(this, view);
    }

    public void RaiseDocumentClosing(DocumentKey document)
    {
        DocumentClosing?.Invoke(this, document);
    }

    public void RaiseModelSynced(DocumentKey model)
    {
        ModelSynced?.Invoke(this, model);
    }

    public FakeSource SourceOf(DocumentKey document)
    {
        return _sources[DocumentOf(document).Source];
    }

    private static ElementDelta Diff(SourceId source, IReadOnlyList<ElementRecord> before, IReadOnlyList<ElementRecord> after)
    {
        var old = before.ToDictionary(element => element.Id);
        var now = after.ToDictionary(element => element.Id);
        var added = after.Where(element => !old.ContainsKey(element.Id)).ToList();
        var modified = after.Where(element => old.TryGetValue(element.Id, out var previous) && previous != element)
            .ToList();
        var deleted = before.Where(element => !now.ContainsKey(element.Id)).Select(element => element.Id).ToList();
        return new ElementDelta(source, [], added, modified, deleted);
    }

    private static async Task<IReadOnlyCollection<long>> AfterAsync(Task gate, IReadOnlyCollection<long> ids)
    {
        await gate.ConfigureAwait(false);
        return ids;
    }

    private static IEnumerable<long> IdsOf(VisibilityPlan plan, DocumentKey document, DocumentSnapshot snapshot)
    {
        foreach (var step in plan.Steps)
        {
            switch (step)
            {
                case ElementsStep elements when elements.Document == document:
                    foreach (var id in elements.Ids)
                    {
                        yield return id;
                    }

                    break;
                case GroupEverywhereStep group:
                    var index = snapshot.GroupIndexOf(group.GroupId);
                    foreach (var row in snapshot.Rows.Where(row => row.GroupIndex == index))
                    {
                        yield return row.Id;
                    }

                    break;
            }
        }
    }

    private async Task<DocumentSnapshot> BuildAfterAsync(
        Task gate,
        SourceId source,
        IProgress<SnapshotProgress>? progress)
    {
        await gate.ConfigureAwait(false);
        return Build(source, progress);
    }

    private DocumentSnapshot Build(SourceId source, IProgress<SnapshotProgress>? progress)
    {
        var snapshot = FakeModelGenerator.BuildSnapshot(_sources[source], VersionOf(source),
            ReverseRebuilds && BuildCount > 0);
        BuildCount++;
        _snapshots[source] = snapshot;
        progress?.Report(new SnapshotProgress(source, snapshot.ElementCount, snapshot.ElementCount));
        return snapshot;
    }

    private IReadOnlyCollection<long> ScopeIds(DocumentKey document, ScopeKind scope)
    {
        var elements = SourceOf(document).Elements;
        return scope switch
        {
            ScopeKind.Selection => _selection.Where(element => element.Document == document)
                .Select(element => element.Id)
                .ToList(),
            ScopeKind.ActiveView => elements.Where(element => element.Id % 3 == 0)
                .Select(element => element.Id)
                .ToList(),
            _ => elements.Select(element => element.Id).ToList()
        };
    }

    private DocumentInfo DocumentOf(DocumentKey key)
    {
        return _documents[key.ModelId].First(document => document.Key == key);
    }

    private DocumentSnapshot SnapshotOf(DocumentKey document)
    {
        return SnapshotOf(DocumentOf(document).Source);
    }

    private DocumentSnapshot SnapshotOf(SourceId source)
    {
        if (!_snapshots.TryGetValue(source, out var snapshot))
        {
            snapshot = FakeModelGenerator.BuildSnapshot(_sources[source]);
            _snapshots[source] = snapshot;
        }

        return snapshot;
    }

    private void RefreshData()
    {
        Data = Data with
        {
            Documents = _documents[_primary].ToList(),
            Sources = new Dictionary<SourceId, FakeSource>(_sources)
        };
    }

    private sealed record FakeVersion(DocumentVersion Version, IReadOnlyList<ElementRecord> Elements);
}
