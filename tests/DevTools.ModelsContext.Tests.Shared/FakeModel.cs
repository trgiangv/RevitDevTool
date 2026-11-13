using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Ports;

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
    private readonly Dictionary<SourceId, DocumentSnapshot> _snapshots = [];
    private List<CustomFilter> _customFilters = [];
    private IReadOnlyCollection<ElementRef> _selection = [];

    public FakeModel(FakeModelData data)
    {
        Data = data;
        View = new ViewInfo(data.CurrentModel, "view-1", "Level 1", false);
    }

    public FakeModel(FakeModelOptions? options = null)
        : this(FakeModelGenerator.Generate(options))
    {
    }

    public FakeModelData Data { get; }

    public ViewInfo View { get; set; }

    public int BuildCount { get; private set; }

    public bool EchoSelection { get; set; } = true;

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

    public TreeSchema Schema => FakeModelGenerator.Schema;

    public Task<IReadOnlyList<DocumentInfo>> GetDocumentsAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(Data.Documents);
    }

    public Task<DocumentSnapshot> BuildSnapshotAsync(
        SourceId source,
        IProgress<SnapshotProgress>? progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fake = Data.Sources[source];
        var snapshot = FakeModelGenerator.BuildSnapshot(fake);
        BuildCount++;
        _snapshots[source] = snapshot;
        progress?.Report(new SnapshotProgress(source, snapshot.ElementCount, snapshot.ElementCount));
        return Task.FromResult(snapshot);
    }

    public Task<IReadOnlyCollection<long>> CollectScopeAsync(
        DocumentKey document,
        ScopeKind scope,
        CancellationToken cancellationToken)
    {
        var elements = Data.SourceOf(document).Elements;
        IReadOnlyCollection<long> ids = scope switch
        {
            ScopeKind.Selection => _selection.Where(element => element.Document == document)
                .Select(element => element.Id)
                .ToList(),
            ScopeKind.ActiveView => elements.Where(element => element.Id % 3 == 0)
                .Select(element => element.Id)
                .ToList(),
            _ => elements.Select(element => element.Id).ToList()
        };
        return Task.FromResult(ids);
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

    public Task<IReadOnlyCollection<ElementRef>> GetSelectionAsync(CancellationToken cancellationToken)
    {
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
        if (!_snapshots.TryGetValue(source, out var snapshot))
        {
            snapshot = FakeModelGenerator.BuildSnapshot(Data.Sources[source]);
            _snapshots[source] = snapshot;
        }

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
            ? Data.SourceOf(document).Elements.Take(10).Select(element => element.Id).ToList()
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

    private DocumentSnapshot SnapshotOf(DocumentKey document)
    {
        var source = Data.Document(document).Source;
        if (!_snapshots.TryGetValue(source, out var snapshot))
        {
            snapshot = FakeModelGenerator.BuildSnapshot(Data.Sources[source]);
            _snapshots[source] = snapshot;
        }

        return snapshot;
    }
}
