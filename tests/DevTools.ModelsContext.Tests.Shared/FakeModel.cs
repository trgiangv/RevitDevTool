using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Parameters;
using DevTools.ModelsContext.Scope;
using DevTools.ModelsContext.Selection;
using System.Globalization;

namespace DevTools.ModelsContext.Tests.Shared;

public sealed class FakeModel : IModelSource, IModelEvents, ISelectionPort, IFilterEvaluator
{
    public const string FakeHostName = "Fake host";
    public const string NotOpen = "This model is not open.";

    private readonly Dictionary<string, List<DocumentInfo>> _documents = new(StringComparer.Ordinal);
    private readonly Dictionary<SourceId, List<FakeVersion>> _history = [];
    private string _primary;
    private readonly Dictionary<SourceId, DocumentSnapshot> _snapshots = [];
    private readonly Dictionary<SourceId, FakeSource> _sources = [];
    private string _active;
    private List<TaskCompletionSource<bool>>? _scopeGates;
    private IReadOnlyCollection<ElementRef> _selection = [];
    private TaskCompletionSource<bool>? _snapshotGate;

    public FakeModel(FakeModelData data)
    {
        _primary = data.CurrentModel.ModelId;
        _active = _primary;
        Data = data;
        AddModel(data);
        View = new ViewInfo(data.CurrentModel, "view-1", "Level 1", ViewKind.Plan);
    }

    public FakeModel(FakeModelOptions? options = null)
        : this(FakeModelGenerator.Generate(options))
    {
    }

    public FakeModelData Data { get; private set; }

    public DocumentKey ActiveModel => DocumentKey.Current(_active);

    public ViewInfo View { get; set; }

    public HashSet<DocumentKey> ViewLinks { get; } = [];

    public HashSet<long> ViewHiddenCategories { get; } = [];

    public HashSet<long>? ViewKeptCategories { get; set; }

    public HashSet<ElementRef> ViewHiddenElements { get; } = [];

    public HashSet<ElementRef> ViewNotDrawn { get; } = [];

    public int BuildCount { get; private set; }

    public int DocumentReads { get; private set; }

    public int SelectionReads { get; private set; }

    public int CatalogReads { get; private set; }

    public bool EchoSelection { get; set; } = true;

    public int ExtraParameters { get; set; }

    public bool ReverseRebuilds { get; set; }

    public bool IsBusy { get; private set; }

    public Dictionary<SourceId, double> UnitFactors { get; } = [];

    public event EventHandler? DocumentsChanged;

    public event EventHandler<ElementDelta>? ElementsChanged;

    public event EventHandler<IReadOnlyCollection<ElementRef>>? SelectionChanged;

    public event EventHandler<ViewInfo>? ViewActivated;

    public event EventHandler<DocumentKey>? DocumentClosing;

    public event EventHandler<DocumentKey>? ActiveModelChanged;

    public event EventHandler<DocumentKey>? ModelSynced;

    public event EventHandler<LinkStatusChange>? LinkStatusChanged;

    public event EventHandler<bool>? BusyChanged;

    public event EventHandler<SourceId>? ParameterDefinitionsChanged;

    public event EventHandler<DocumentKey>? ViewFiltersRemoved;

    public event EventHandler<ModelRename>? ModelRenamed;

    public string HostName => FakeHostName;

    public Task<IReadOnlyList<DocumentInfo>> GetDocumentsAsync(CancellationToken cancellationToken)
    {
        DocumentReads++;
        IReadOnlyList<DocumentInfo> documents = _documents.TryGetValue(_active, out var list) ? list.ToList() : [];
        return Task.FromResult(documents);
    }

    public TaskCompletionSource<bool>? ViewLinksGate { get; set; }

    public async Task<IReadOnlyCollection<DocumentKey>> GetViewLinksAsync(CancellationToken cancellationToken)
    {
        if (ViewLinksGate is { } gate)
        {
            using (cancellationToken.Register(() => gate.TrySetCanceled()))
            {
                await gate.Task;
            }
        }

        return ViewLinks.Where(key => key.ModelId == _active).ToList();
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
        if (_scopeGates is not { } gates)
        {
            return Task.FromResult(ids);
        }

        var gate = new TaskCompletionSource<bool>();
        gates.Add(gate);
        return AfterAsync(gate.Task, ids);
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
            .Select(group => new GroupCount(group.Key, types[group.First().TypeId].GroupName, group.Count(),
                types[group.First().TypeId].ParentId))
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
        CatalogReads++;
        var catalog = FakeParameters.Catalog(_sources[source]);
        if (source == FakeModelGenerator.CurrentSource && ExtraCatalog.Count > 0)
        {
            catalog = [.. catalog, .. ExtraCatalog];
        }

        if (ExtraParameters > 0)
        {
            var start = _sources.Keys.ToList().IndexOf(source) * ExtraParameters * 3 / 4;
            catalog = [.. catalog, .. FakeParameters.Extra(_sources[source], ExtraParameters, start)];
        }

        return Task.FromResult(catalog);
    }

    public TaskCompletionSource<bool>? UniqueIdGate { get; set; }

    public async Task<IReadOnlyDictionary<long, string>> GetUniqueIdsAsync(
        DocumentKey document,
        IReadOnlyCollection<long> ids,
        CancellationToken cancellationToken)
    {
        if (UniqueIdGate is { } gate)
        {
            using (cancellationToken.Register(() => gate.TrySetCanceled()))
            {
                await gate.Task;
            }
        }

        var source = DocumentOf(document).Source;
        return ids.Distinct().ToDictionary(id => id,
            id => string.Format(CultureInfo.InvariantCulture, "{0}-{1:x8}", source.Value, id));
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

    public IReadOnlyCollection<ElementRef> PickCandidates { get; set; } = [];

    public bool CancelPick { get; set; }

    public int PickCalls { get; private set; }

    public IReadOnlyCollection<DocumentKey> LastPickLinks { get; private set; } = [];

    public IReadOnlyCollection<long> LastPickCategories { get; private set; } = [];

    public TaskCompletionSource<bool>? PickGate { get; set; }

    public async Task<PickResult> PickAsync(
        IReadOnlyCollection<DocumentKey> links,
        IReadOnlyCollection<long> categoryIds,
        CancellationToken cancellationToken)
    {
        PickCalls++;
        LastPickLinks = links;
        LastPickCategories = categoryIds;
        if (PickGate is { } gate)
        {
            using (cancellationToken.Register(() => gate.TrySetCanceled()))
            {
                await gate.Task;
            }
        }

        if (CancelPick)
        {
            return PickResult.Cancel;
        }

        var picked = PickCandidates.Where(element => links.Contains(element.Document)).ToList();
        if (picked.Count > 0)
        {
            await SelectAsync(picked, cancellationToken);
        }

        return PickResult.Of(picked);
    }

    public Task<IReadOnlyCollection<long>> EvaluateAsync(
        DocumentKey document,
        FilterDefinition filter,
        IReadOnlyCollection<long>? scope,
        CancellationToken cancellationToken)
    {
        if (!DocumentOf(document).IsLoaded)
        {
            throw new InvalidOperationException(NotOpen);
        }

        var snapshot = SnapshotOf(document);
        var host = Data.Documents.FirstOrDefault(info => info.Key == DocumentKey.Current(document.ModelId));
        var factor = host is not null && UnitFactors.TryGetValue(host.Source, out var hostFactor) ? hostFactor : 1;
        var groups = filter.Groups.ToHashSet();
        var inScope = scope as ISet<long> ?? scope?.ToHashSet();
        IReadOnlyCollection<long> ids = snapshot.Rows
            .Where(row => groups.Count == 0 || groups.Contains(snapshot.Groups[row.GroupIndex].GroupId))
            .Where(row => inScope is null || inScope.Contains(row.Id))
            .Where(row => FakeFilterMatcher.Matches(snapshot, row, filter.Root, Data.Levels, factor))
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
        var numbers = new Dictionary<string, NumberRange>(StringComparer.Ordinal);
        var display = DisplayFactors.TryGetValue(source, out var factor) ? factor : 1;
        var isNumber = FakeParameters.KindOf(parameter) == ValueKind.Number;
        foreach (var row in snapshot.Rows)
        {
            if (wanted.Count > 0 && !wanted.Contains(snapshot.Groups[row.GroupIndex].GroupId))
            {
                continue;
            }

            total++;
            if (!FakeParameters.TryGet(snapshot, row, parameter, Data.Levels, out var value))
            {
                continue;
            }

            if (isNumber && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var stored))
            {
                value = (stored / display).ToString(CultureInfo.InvariantCulture);
                numbers[value] = numbers.TryGetValue(value, out var range) ? range.With(stored) : NumberRange.Of(stored);
            }

            values[value] = values.TryGetValue(value, out var count) ? count + 1 : 1;
        }

        return Task.FromResult(new ParameterValueCounts(total, values, numbers));
    }

    public Dictionary<SourceId, double> DisplayFactors { get; } = [];

    public List<ParameterInfo> ExtraCatalog { get; } = [];

    public Dictionary<string, string> Examples { get; } = new(StringComparer.Ordinal)
    {
        [FakeParameters.LengthSpec] = "300 mm",
        [FakeParameters.AngleSpec] = "60°"
    };

    public List<DocumentKey> ExampleRequests { get; } = [];

    public Task<string?> FormatExampleAsync(DocumentKey model, string spec, CancellationToken cancellationToken)
    {
        ExampleRequests.Add(model);
        return Task.FromResult(Examples.TryGetValue(spec, out var example) ? example : null);
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
        View = new ViewInfo(model, "view-1", "Level 1", ViewKind.Plan);
        _selection = [];
        ActiveModelChanged?.Invoke(this, model);
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

    public void RemoveDocument(DocumentKey link)
    {
        _documents[link.ModelId].RemoveAll(document => document.Key == link);
        RefreshData();
        DocumentsChanged?.Invoke(this, EventArgs.Empty);
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

    public int HeldScopes => _scopeGates?.Count ?? 0;

    public void HoldScopes()
    {
        _scopeGates = [];
    }

    public void ReleaseScope(int index)
    {
        _scopeGates![index].TrySetResult(true);
    }

    public void ReleaseScopes()
    {
        var gates = _scopeGates ?? [];
        _scopeGates = null;
        foreach (var gate in gates)
        {
            gate.TrySetResult(true);
        }
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

    public void RaiseParameterDefinitionsChanged(SourceId source)
    {
        ParameterDefinitionsChanged?.Invoke(this, source);
    }

    public void RaiseModelSynced(DocumentKey model)
    {
        ModelSynced?.Invoke(this, model);
    }

    public ModelRename RenameModel(string newId)
    {
        var oldId = _active;
        var source = DocumentOf(DocumentKey.Current(oldId)).Source;
        var rename = new ModelRename(oldId, newId, source, source);
        _documents[newId] = _documents[oldId].Select(rename.Map).ToList();
        _documents.Remove(oldId);
        if (_primary == oldId)
        {
            _primary = newId;
        }

        _active = newId;
        View = rename.Map(View);
        _selection = _selection.Select(rename.Map).ToList();
        RefreshData();
        Data = Data with { CurrentModel = rename.Map(Data.CurrentModel) };
        ModelRenamed?.Invoke(this, rename);
        return rename;
    }

    public void RaiseViewFiltersRemoved(DocumentKey model)
    {
        ViewFiltersRemoved?.Invoke(this, model);
    }

    public FakeSource SourceOf(DocumentKey document)
    {
        return _sources[DocumentOf(document).Source];
    }

    public DocumentSnapshot SnapshotOf(DocumentKey document)
    {
        return SnapshotOf(DocumentOf(document).Source);
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
            ScopeKind.ActiveView => elements.Where(element => element.Id % 3 == 0 && IsDrawn(document, element))
                .Select(element => element.Id)
                .ToList(),
            _ => elements.Select(element => element.Id).ToList()
        };
    }

    private bool IsDrawn(DocumentKey document, ElementRecord element)
    {
        if (ViewHiddenElements.Contains(new ElementRef(document, element.Id)))
        {
            return false;
        }

        if (ViewHiddenCategories.Count == 0 && ViewKeptCategories is null)
        {
            return true;
        }

        var snapshot = SnapshotOf(document);
        var category = snapshot.Groups[snapshot.Types[snapshot.TypeIndexOf(element.TypeId)].GroupIndex].GroupId;
        return !ViewHiddenCategories.Contains(category) && ViewKeptCategories?.Contains(category) != false;
    }

    private DocumentInfo DocumentOf(DocumentKey key)
    {
        return _documents[key.ModelId].First(document => document.Key == key);
    }

    private DocumentVersion VersionOf(SourceId source)
    {
        return _history[source][_history[source].Count - 1].Version;
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
