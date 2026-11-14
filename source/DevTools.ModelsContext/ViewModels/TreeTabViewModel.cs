using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTools.ModelsContext.Engine;
using DevTools.ModelsContext.Model;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DevTools.ModelsContext.ViewModels;

public sealed record RowColor(TreeRow Row, RgbColor Color);

public sealed class TreeTabViewModel : ObservableObject, IDisposable
{
    public const int InstancePageSize = 200;
    public const int OpenPathLimit = 50;
    public const int SyncPauseLimit = 5000;
    public const string AllModelsText = "All models";
    public const string TooManyHint = "Too many to expand · Expand all";

    public static readonly TimeSpan SyncDelay = TimeSpan.FromMilliseconds(200);
    public static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(150);

    private static readonly Regex IdToken = new(@"^(?:(\d+)@)?(\d+)$", RegexOptions.CultureInvariant);

    private readonly ContextBarViewModel _bar;
    private readonly Dictionary<TreeRow, long[]> _instanceIds = [];
    private readonly ContextMenuViewModel _menu;
    private readonly PanelRuntime _runtime;
    private readonly Debouncer _searchDebouncer;
    private readonly List<TreeRow> _highlighted = [];
    private readonly HashSet<TreeRow> _selectedRows = [];
    private readonly Debouncer _sendDebouncer;
    private readonly Dictionary<DocumentKey, DocumentState> _states = [];
    private readonly VisibilityController _visibility;
    private ContextData _data = ContextData.Empty;
    private bool _isSyncOn = true;
    private bool _isSyncPaused;
    private bool _isUpdating;
    private string? _message;
    private TreeState? _pending;
    private bool _revealPending;
    private List<TreeRow> _roots = [];
    private HashSet<TreeRow>? _searchHits;
    private HashSet<TreeRow> _searchMatches = [];
    private string _searchText = string.Empty;
    private HashSet<ElementRef> _selectedElements = [];
    private string? _syncHint;
    private CancellationToken _token;

    internal TreeTabViewModel(
        PanelRuntime runtime,
        VisibilityController visibility,
        ContextBarViewModel bar,
        ContextMenuViewModel menu)
    {
        _runtime = runtime;
        _visibility = visibility;
        _bar = bar;
        _menu = menu;
        _sendDebouncer = new Debouncer(runtime.Services.Scheduler, SyncDelay,
            () => _ = _runtime.RunAsync(SendSelectionAsync));
        _searchDebouncer = new Debouncer(runtime.Services.Scheduler, SearchDelay,
            () => _ = _runtime.RunAsync(ApplySearchAsync));
        ToggleExpandCommand = new AsyncRelayCommand<TreeRow?>(row => Run(() => ToggleExpandAsync(row)));
        LoadMoreCommand = new AsyncRelayCommand<TreeRow?>(row => Run(() => LoadMoreAsync(row)));
        ToggleEyeCommand = new AsyncRelayCommand<TreeRow?>(row => Run(() => ToggleEyeAsync(row)));
        IsolateCommand = new AsyncRelayCommand<TreeRow?>(row => Run(() => ActAsync(row, VisibilityAction.Isolate)));
        HalftoneCommand = new AsyncRelayCommand<TreeRow?>(row => Run(() => ActAsync(row, VisibilityAction.Halftone)));
        ColorCommand = new AsyncRelayCommand<RowColor?>(request => Run(() => ColorAsync(request)));
        ClearSelectionCommand = new AsyncRelayCommand(() => Run(ClearSelectionAsync));
        ShowInTreeCommand = new RelayCommand(ShowInTree);
        ExpandAllCommand = new AsyncRelayCommand(() => Run(ExpandAllAsync));
        _visibility.Changed += (_, _) => OnVisibilityChanged();
        _visibility.VisibleIds = VisibleIdsOf;
    }

    public event EventHandler? RowSelectionChanged;

    public event EventHandler<TreeRow>? ScrollRequested;

    public RangeObservableCollection<TreeRow> Rows { get; } = [];

    public IReadOnlyCollection<TreeRow> SelectedRows => _selectedRows;

    public IReadOnlyCollection<ElementRef> SelectedElements => _selectedElements;

    public IAsyncRelayCommand<TreeRow?> ToggleExpandCommand { get; }

    public IAsyncRelayCommand<TreeRow?> LoadMoreCommand { get; }

    public IAsyncRelayCommand<TreeRow?> ToggleEyeCommand { get; }

    public IAsyncRelayCommand<TreeRow?> IsolateCommand { get; }

    public IAsyncRelayCommand<TreeRow?> HalftoneCommand { get; }

    public IAsyncRelayCommand<RowColor?> ColorCommand { get; }

    public IAsyncRelayCommand ClearSelectionCommand { get; }

    public IRelayCommand ShowInTreeCommand { get; }

    public IAsyncRelayCommand ExpandAllCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty))
            {
                _searchDebouncer.Trigger();
            }
        }
    }

    public bool IsSyncOn
    {
        get => _isSyncOn;
        set
        {
            if (!SetProperty(ref _isSyncOn, value))
            {
                return;
            }

            IsSyncPaused = false;
            OnPropertyChanged(nameof(IsSyncing));
            if (value)
            {
                _sendDebouncer.Trigger();
            }
        }
    }

    public bool IsSyncVisible => _bar.Scope != ScopeKind.Selection;

    public bool IsSyncPaused
    {
        get => _isSyncPaused;
        private set
        {
            if (SetProperty(ref _isSyncPaused, value))
            {
                OnPropertyChanged(nameof(IsSyncing));
            }
        }
    }

    public bool IsSyncing => IsSyncOn && IsSyncVisible && !IsSyncPaused;

    public string? SyncHint
    {
        get => _syncHint;
        private set => SetProperty(ref _syncHint, value);
    }

    public int SelectedCount => _selectedElements.Count;

    public bool HasSelection => SelectedCount > 0;

    public string StatusText => IsUpdating
        ? "Updating…"
        : string.Format(CultureInfo.CurrentCulture, "{0:N0} selected", SelectedCount);

    public bool CanShowInTree => IsSyncVisible && _selectedElements.Any(element => _data.Find(element.Document) is null);

    public bool IsUpdating
    {
        get => _isUpdating;
        internal set
        {
            if (SetProperty(ref _isUpdating, value))
            {
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public string? Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public void Dispose()
    {
        _sendDebouncer.Dispose();
        _searchDebouncer.Dispose();
    }

    public async Task ExpandAsync(TreeRow row)
    {
        if (row.IsExpanded || !row.HasChildren)
        {
            return;
        }

        await EnsureChildrenAsync(row);
        row.IsExpanded = true;
        ApplyState(row);
        var index = Rows.IndexOf(row);
        if (index < 0)
        {
            return;
        }

        var visible = new List<TreeRow>();
        Flatten(row.Children!, IsUnderMatch(row), visible);
        Rows.InsertRange(index + 1, visible);
        if (await ReadHiddenAsync())
        {
            Publish();
        }
    }

    public void Collapse(TreeRow row)
    {
        if (!row.IsExpanded)
        {
            return;
        }

        row.IsExpanded = false;
        var index = Rows.IndexOf(row);
        if (index < 0)
        {
            return;
        }

        var end = index + 1;
        while (end < Rows.Count && Rows[end].Level > row.Level)
        {
            end++;
        }

        Rows.ReplaceRange(index, end - index, [row]);
    }

    public void SetSelectedRows(IEnumerable<TreeRow> rows)
    {
        _selectedRows.Clear();
        _selectedRows.UnionWith(rows);
        if (IsSyncOn && IsSyncVisible && !_runtime.IsBusy)
        {
            _sendDebouncer.Trigger();
        }
    }

    public IReadOnlyList<ElementRef> ElementsOf(IEnumerable<TreeRow> rows)
    {
        var elements = new HashSet<ElementRef>();
        foreach (var row in rows)
        {
            switch (row.Kind)
            {
                case TreeRowKind.AllModelsGroup:
                    foreach (var document in _data.Documents)
                    {
                        var index = document.Snapshot.GroupIndexOf(row.Id);
                        if (index >= 0)
                        {
                            elements.UnionWith(document.IdsUnder(RowPath.ForGroup(index))
                                .Select(id => new ElementRef(document.Key, id)));
                        }
                    }

                    break;
                case TreeRowKind.LinkModel when row.Document is { } link:
                    elements.Add(new ElementRef(link.CurrentModel, link.LinkInstanceId));
                    break;
                case TreeRowKind.Group or TreeRowKind.Family or TreeRowKind.Type when DocumentOf(row) is { } scope:
                    elements.UnionWith(scope.IdsUnder(row.Path).Select(id => new ElementRef(scope.Key, id)));
                    break;
                case TreeRowKind.Instance when row.Document is { } key:
                    elements.Add(new ElementRef(key, row.Id));
                    break;
            }
        }

        return elements.ToList();
    }

    public void HighlightEyeTargets(TreeRow? row)
    {
        ClearHighlight();
        if (row is null || row.Eye is RowEye.None or RowEye.HiddenByAllModels)
        {
            return;
        }

        var targets = new HashSet<TreeRow>(TargetsFor(row));
        var groups = new HashSet<long>(targets.Where(target => target.Kind == TreeRowKind.AllModelsGroup)
            .Select(target => target.Id));
        foreach (var visible in Rows)
        {
            if (IsAffected(visible, targets, groups))
            {
                visible.IsHighlighted = true;
                _highlighted.Add(visible);
            }
        }
    }

    public void ClearHighlight()
    {
        foreach (var row in _highlighted)
        {
            row.IsHighlighted = false;
        }

        _highlighted.Clear();
    }

    public string? ReasonFor(TreeRow row, VisibilityAction action)
    {
        return _visibility.Plan(VisibilityRequest.ForRows(action, row)).DisabledReason;
    }

    public void OpenMenu(TreeRow? clicked)
    {
        if (clicked is not null && !_selectedRows.Contains(clicked))
        {
            SetSelectedRows([clicked]);
            RowSelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        var rows = _selectedRows.Where(row => !row.IsHeader && row.Kind != TreeRowKind.MorePage).ToList();
        var hidden = rows.Count > 0 && rows.All(row => row.Eye == RowEye.Hidden);
        _menu.Open(new MenuTarget(rows, [], () => ElementsOf(rows), IsSyncing, hidden, _token));
    }

    internal TreeState Capture()
    {
        if (_pending is { } pending)
        {
            return pending with { IsSyncOn = _isSyncOn };
        }

        var expanded = new HashSet<string>(StringComparer.Ordinal);
        CollectExpanded(_roots, expanded);
        return new TreeState(expanded, new HashSet<string>(_roots.Select(KeyOf), StringComparer.Ordinal), _isSyncOn);
    }

    internal void Reset(TreeState state)
    {
        _pending = state;
        ClearHighlight();
        _data = ContextData.Empty;
        _roots = [];
        _states.Clear();
        _instanceIds.Clear();
        _selectedRows.Clear();
        _selectedElements = [];
        _searchHits = null;
        _searchMatches = [];
        _revealPending = false;
        SyncHint = null;
        IsSyncPaused = false;
        if (_isSyncOn != state.IsSyncOn)
        {
            _isSyncOn = state.IsSyncOn;
            OnPropertyChanged(nameof(IsSyncOn));
            OnPropertyChanged(nameof(IsSyncing));
        }

        _sendDebouncer.Cancel();
        Rows.ReplaceAll([]);
        RaiseSelectionCounts();
        RowSelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    internal void ShowQuickCounts(IReadOnlyList<QuickDocument> documents, DocumentKey? currentModel)
    {
        _pending ??= Capture();
        var state = _pending;
        _data = ContextData.Empty;
        _states.Clear();
        _instanceIds.Clear();
        _selectedRows.Clear();
        _roots = [];
        if (documents.Count > 0)
        {
            var current = currentModel ?? documents[0].Key.CurrentModel;
            var header = new TreeRow(TreeRowKind.AllModelsHeader, AllModelsText, document: current)
            {
                HasChildren = true,
                Key = RowKey(TreeRowKind.AllModelsHeader, current)
            };
            var merged = documents.SelectMany(document => document.Groups)
                .GroupBy(group => group.GroupId)
                .Select(group => new GroupCount(group.Key, group.First().Name, group.Sum(item => item.Count)))
                .ToList();
            header.Children = QuickGroupRows(header, TreeRowKind.AllModelsGroup, current, merged);
            header.Count = merged.Sum(group => group.Count);
            _roots.Add(header);
            foreach (var document in documents.OrderBy(document => !document.Key.IsCurrentModel))
            {
                var root = RootRow(document.Key, document.Label);
                root.Children = QuickGroupRows(root, TreeRowKind.Group, document.Key, document.Groups);
                root.Count = document.Groups.Sum(group => group.Count);
                _roots.Add(root);
            }

            foreach (var root in _roots)
            {
                root.IsExpanded = state.Expanded.Contains(root.Key) || !state.Roots.Contains(root.Key);
            }
        }

        var visible = new List<TreeRow>();
        foreach (var root in _roots)
        {
            visible.Add(root);
            if (root.IsExpanded)
            {
                visible.AddRange(root.Children!);
            }
        }

        Rows.ReplaceAll(visible);
    }

    internal async Task LoadAsync(ContextData data, CancellationToken cancellationToken)
    {
        var state = _pending ?? Capture();
        _data = data;
        _token = cancellationToken;
        _instanceIds.Clear();
        _selectedRows.Clear();
        _roots = [];
        await ReadHiddenAsync();
        ComputeStates();
        _roots = BuildRoots();
        foreach (var root in _roots.Where(root => state.Expanded.Contains(KeyOf(root)) || !state.Roots.Contains(KeyOf(root))))
        {
            await RestoreAsync(root, state.Expanded);
        }

        _pending = null;

        if (_searchText.Trim().Length > 0)
        {
            await CollectSearchAsync();
        }

        if (_revealPending)
        {
            _revealPending = false;
            await RevealSelectionAsync(OpenPathLimit);
        }

        await ReadHiddenAsync();
        OnPropertyChanged(nameof(IsSyncVisible));
        OnPropertyChanged(nameof(IsSyncing));
        OnPropertyChanged(nameof(CanShowInTree));
        Publish();
        RowSelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    internal async Task ApplyHostSelectionAsync(IReadOnlyCollection<ElementRef> incoming)
    {
        _selectedElements = [.. incoming];
        SyncHint = null;
        RaiseSelectionCounts();
        ComputeSelected();
        if (!IsSyncOn || !IsSyncVisible)
        {
            Publish();
            return;
        }

        if (_selectedElements.Count > SyncPauseLimit)
        {
            IsSyncPaused = true;
            Publish();
            return;
        }

        IsSyncPaused = false;
        _selectedRows.Clear();
        if (_selectedElements.Count <= OpenPathLimit)
        {
            await RevealSelectionAsync(OpenPathLimit);
        }
        else
        {
            SyncHint = TooManyHint;
        }

        await ReadHiddenAsync();
        Publish();
        RowSelectionChanged?.Invoke(this, EventArgs.Empty);
        if (Rows.FirstOrDefault(_selectedRows.Contains) is { } first)
        {
            ScrollRequested?.Invoke(this, first);
        }
    }

    private static string KeyOf(TreeRow row)
    {
        return row.Key;
    }

    private static string RowKey(
        TreeRowKind kind,
        DocumentKey? document,
        long group = RowPath.Unset,
        string family = "",
        long type = RowPath.Unset,
        long element = RowPath.Unset)
    {
        return string.Join("|", (int)kind, document?.ToString() ?? string.Empty,
            group.ToString(CultureInfo.InvariantCulture), family, type.ToString(CultureInfo.InvariantCulture),
            element.ToString(CultureInfo.InvariantCulture));
    }

    private static TreeRow RootRow(DocumentKey key, string label)
    {
        return key.IsCurrentModel
            ? new TreeRow(TreeRowKind.CurrentModelHeader, ContextBarViewModel.CurrentModelLabel, document: key)
            {
                HasChildren = true,
                Key = RowKey(TreeRowKind.CurrentModelHeader, key)
            }
            : new TreeRow(TreeRowKind.LinkModel, label, document: key, id: key.LinkInstanceId)
            {
                HasChildren = true,
                Key = RowKey(TreeRowKind.LinkModel, key)
            };
    }

    private static List<TreeRow> QuickGroupRows(
        TreeRow parent,
        TreeRowKind kind,
        DocumentKey document,
        IEnumerable<GroupCount> groups)
    {
        return Sorted(groups.Where(group => group.Count > 0).Select(group =>
            new TreeRow(kind, group.Name, parent, id: group.GroupId)
            {
                Count = group.Count,
                DisabledReason = VisibilityReasons.Updating,
                Key = RowKey(kind, document, group.GroupId)
            }));
    }

    private static void CollectExpanded(IEnumerable<TreeRow> rows, HashSet<string> keys)
    {
        foreach (var row in rows)
        {
            if (row.IsExpanded)
            {
                keys.Add(KeyOf(row));
            }

            if (row.Children is { } children)
            {
                CollectExpanded(children, keys);
            }
        }
    }

    private static int[] KeyPath(RowPath path, int depth)
    {
        if (path.Group == RowPath.Any)
        {
            return [];
        }

        return depth switch
        {
            1 => [path.Group],
            2 => path.Type == RowPath.Any ? [path.Group] : [path.Group, path.Type],
            _ => path.Family == RowPath.Any ? [path.Group]
                : path.Type == RowPath.Any ? [path.Group, path.Family]
                : [path.Group, path.Family, path.Type]
        };
    }

    private static RowEye EyeOf(int hidden, int total)
    {
        if (hidden <= 0)
        {
            return RowEye.Shown;
        }

        return hidden >= total ? RowEye.Hidden : RowEye.Partly;
    }

    private static bool Matches(TreeRow row, string text)
    {
        return row.Text.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static IEnumerable<int> KeysOf(CountNode? scope, CountNode? hidden)
    {
        var keys = new HashSet<int>();
        if (scope is not null)
        {
            keys.UnionWith(scope.Children.Keys);
        }

        if (hidden is not null)
        {
            keys.UnionWith(hidden.Children.Keys);
        }

        return keys;
    }

    private static List<TreeRow> Sorted(IEnumerable<TreeRow> rows)
    {
        return rows.OrderBy(row => row.Text, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private Task Run(Func<Task> work)
    {
        Message = null;
        return _runtime.RunAsync(work);
    }

    private async Task ToggleExpandAsync(TreeRow? row)
    {
        if (row is null)
        {
            return;
        }

        if (row.IsExpanded)
        {
            Collapse(row);
        }
        else
        {
            await ExpandAsync(row);
        }
    }

    private async Task LoadMoreAsync(TreeRow? more)
    {
        if (more is not { Kind: TreeRowKind.MorePage, Parent: { } parent })
        {
            return;
        }

        var page = await AppendPageAsync(parent);
        foreach (var row in page)
        {
            ApplyState(row);
        }

        var index = Rows.IndexOf(more);
        if (index >= 0)
        {
            Rows.ReplaceRange(index, 1, page);
        }

        if (await ReadHiddenAsync())
        {
            Publish();
        }
    }

    private async Task ToggleEyeAsync(TreeRow? row)
    {
        if (row is null || row.Eye is RowEye.None or RowEye.HiddenByAllModels)
        {
            return;
        }

        var action = row.Eye == RowEye.Hidden ? VisibilityAction.Show : VisibilityAction.Hide;
        Message = await _visibility.RunAsync(new VisibilityRequest(action, TargetsFor(row), []), _token);
    }

    private async Task ActAsync(TreeRow? row, VisibilityAction action)
    {
        if (row is null)
        {
            return;
        }

        Message = await _visibility.RunAsync(new VisibilityRequest(action, TargetsFor(row), []), _token);
    }

    private static bool IsAffected(TreeRow row, HashSet<TreeRow> targets, HashSet<long> groups)
    {
        for (var current = row; current is not null; current = current.Parent)
        {
            if (targets.Contains(current) || (current.Kind == TreeRowKind.Group && groups.Contains(current.Id)))
            {
                return true;
            }
        }

        return false;
    }

    private async Task ColorAsync(RowColor? request)
    {
        if (request is not { } color)
        {
            return;
        }

        Message = await _visibility.RunAsync(
            new VisibilityRequest(VisibilityAction.Color, TargetsFor(color.Row), [], color.Color), _token);
    }

    private List<TreeRow> TargetsFor(TreeRow row)
    {
        return _selectedRows.Contains(row)
            ? _selectedRows.Where(selected => !selected.IsHeader && selected.Kind != TreeRowKind.MorePage).ToList()
            : [row];
    }

    private async Task ClearSelectionAsync()
    {
        _selectedRows.Clear();
        _selectedElements.Clear();
        SyncHint = null;
        await _runtime.SelectAsync([], _token);
        RaiseSelectionCounts();
        ComputeSelected();
        Publish();
        RowSelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ShowInTree()
    {
        var missing = _selectedElements.Select(element => element.Document)
            .Where(document => _data.Find(document) is null)
            .Distinct()
            .ToList();
        if (missing.Count == 0)
        {
            return;
        }

        _revealPending = true;
        _bar.Tick(missing);
    }

    private async Task ExpandAllAsync()
    {
        SyncHint = null;
        _selectedRows.Clear();
        await RevealSelectionAsync(int.MaxValue);
        await ReadHiddenAsync();
        Publish();
        RowSelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task SendSelectionAsync()
    {
        if (!IsSyncOn || !IsSyncVisible || _runtime.IsBusy)
        {
            return;
        }

        var elements = ElementsOf(_selectedRows);
        if (elements.Count > SyncPauseLimit)
        {
            IsSyncPaused = true;
            return;
        }

        IsSyncPaused = false;
        await _runtime.SelectAsync(elements, _token);
        _selectedElements = [.. elements];
        SyncHint = null;
        RaiseSelectionCounts();
        ComputeSelected();
        Publish();
        RowSelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RaiseSelectionCounts()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(CanShowInTree));
    }

    private async Task RevealSelectionAsync(int limit)
    {
        foreach (var element in _selectedElements.Take(limit))
        {
            if (await RevealAsync(element) is { } row)
            {
                _selectedRows.Add(row);
            }
        }
    }

    private async Task<TreeRow?> RevealAsync(ElementRef element)
    {
        if (_data.Find(element.Document) is not { } scope || !scope.Snapshot.TryGetRow(element.Id, out var target))
        {
            return null;
        }

        var row = _roots.FirstOrDefault(root =>
            root.Kind is TreeRowKind.CurrentModelHeader or TreeRowKind.LinkModel && root.Document == element.Document);
        while (row is not null)
        {
            await EnsureChildrenAsync(row);
            row.IsExpanded = true;
            var next = row.Children!.FirstOrDefault(child => child.Kind == TreeRowKind.Instance
                ? child.Id == element.Id
                : child.Kind != TreeRowKind.MorePage && scope.Snapshot.IsUnder(target, child.Path));
            while (next is null && row.Children!.Count > 0 && row.Children[row.Children.Count - 1].Kind == TreeRowKind.MorePage)
            {
                var page = await AppendPageAsync(row);
                next = page.FirstOrDefault(child => child.Kind == TreeRowKind.Instance && child.Id == element.Id);
            }

            if (next is { Kind: TreeRowKind.Instance })
            {
                return next;
            }

            row = next;
        }

        return null;
    }

    private async Task RestoreAsync(TreeRow row, HashSet<string> expanded)
    {
        if (!row.HasChildren)
        {
            return;
        }

        await EnsureChildrenAsync(row);
        row.IsExpanded = true;
        foreach (var child in row.Children!.Where(child => expanded.Contains(KeyOf(child))))
        {
            await RestoreAsync(child, expanded);
        }
    }

    private List<TreeRow> BuildRoots()
    {
        var roots = new List<TreeRow>();
        if (_data.Documents.Count == 0)
        {
            return roots;
        }

        var current = _data.CurrentModel ?? _data.Documents[0].Key.CurrentModel;
        roots.Add(new TreeRow(TreeRowKind.AllModelsHeader, AllModelsText, document: current)
        {
            HasChildren = true,
            Key = RowKey(TreeRowKind.AllModelsHeader, current)
        });
        roots.AddRange(_data.Documents.OrderBy(document => !document.Key.IsCurrentModel)
            .Select(document => RootRow(document.Key, document.Label)));
        return roots;
    }

    private async Task EnsureChildrenAsync(TreeRow row)
    {
        if (row.ChildrenLoaded)
        {
            return;
        }

        var scope = DocumentOf(row);
        var depth = scope?.Snapshot.GroupingDepth ?? 0;
        row.Children = row.Kind switch
        {
            TreeRowKind.AllModelsHeader => AllModelsRows(row),
            TreeRowKind.CurrentModelHeader or TreeRowKind.LinkModel when scope is not null => GroupRows(row, scope),
            TreeRowKind.Group when depth == 3 && scope is not null => FamilyRows(row, scope),
            TreeRowKind.Group when depth == 2 && scope is not null => TypeRows(row, scope),
            TreeRowKind.Family when scope is not null => TypeRows(row, scope),
            TreeRowKind.Group or TreeRowKind.Type when scope is not null => await InstanceRowsAsync(row, scope),
            _ => []
        };
    }

    private List<TreeRow> AllModelsRows(TreeRow header)
    {
        var groups = _data.MergedGroups().ToList();
        var known = groups.Select(group => group.GroupId).ToHashSet();
        foreach (var document in _data.Documents)
        {
            foreach (var group in document.Snapshot.Groups)
            {
                if (_visibility.IsGroupHidden(group.GroupId) && known.Add(group.GroupId))
                {
                    groups.Add((group.GroupId, document.Snapshot.Names[group.NameIndex], 0));
                }
            }
        }

        return Sorted(groups.Select(group => new TreeRow(TreeRowKind.AllModelsGroup, group.Name, header,
            id: group.GroupId)
        {
            Count = group.Count,
            Key = RowKey(TreeRowKind.AllModelsGroup, header.Document, group.GroupId)
        }));
    }

    private List<TreeRow> GroupRows(TreeRow parent, DocumentScope scope)
    {
        var snapshot = scope.Snapshot;
        var state = _states[scope.Key];
        return Sorted(KeysOf(scope.Counts, state.Hidden).Select(index =>
        {
            var groupId = snapshot.Groups[index].GroupId;
            return new TreeRow(TreeRowKind.Group, snapshot.GroupName(index), parent, path: RowPath.ForGroup(index),
                id: groupId)
            {
                HasChildren = true,
                Key = RowKey(TreeRowKind.Group, scope.Key, groupId)
            };
        }));
    }

    private List<TreeRow> FamilyRows(TreeRow parent, DocumentScope scope)
    {
        var keys = KeyPath(parent.Path, 3);
        var group = parent.Path.Group;
        var groupId = scope.Snapshot.Groups[group].GroupId;
        return Sorted(KeysOf(scope.Counts.Find(keys), _states[scope.Key].Hidden.Find(keys)).Select(family =>
            new TreeRow(TreeRowKind.Family, scope.Snapshot.Names[family], parent,
                path: RowPath.ForFamily(group, family))
            {
                HasChildren = true,
                Key = RowKey(TreeRowKind.Family, scope.Key, groupId, scope.Snapshot.Names[family])
            }));
    }

    private List<TreeRow> TypeRows(TreeRow parent, DocumentScope scope)
    {
        var snapshot = scope.Snapshot;
        var keys = KeyPath(parent.Path, snapshot.GroupingDepth);
        var groupId = snapshot.Groups[parent.Path.Group].GroupId;
        var family = parent.Path.Family == RowPath.Any ? string.Empty : snapshot.Names[parent.Path.Family];
        return Sorted(KeysOf(scope.Counts.Find(keys), _states[scope.Key].Hidden.Find(keys)).Select(type =>
            new TreeRow(TreeRowKind.Type, snapshot.TypeName(type), parent,
                path: RowPath.ForType(parent.Path.Group, parent.Path.Family, type))
            {
                HasChildren = true,
                Key = RowKey(TreeRowKind.Type, scope.Key, groupId, family, snapshot.Types[type].TypeId)
            }));
    }

    private async Task<IReadOnlyList<TreeRow>> InstanceRowsAsync(TreeRow parent, DocumentScope scope)
    {
        var state = _states[scope.Key];
        var ids = new HashSet<long>(scope.IdsUnder(parent.Path));
        foreach (var id in state.HiddenIds)
        {
            if (scope.Snapshot.TryGetRow(id, out var row) && scope.Snapshot.IsUnder(row, parent.Path))
            {
                ids.Add(id);
            }
        }

        _instanceIds[parent] = ids.OrderBy(id => id).ToArray();
        parent.Children = [];
        return await AppendPageAsync(parent);
    }

    private async Task<IReadOnlyList<TreeRow>> AppendPageAsync(TreeRow parent)
    {
        if (!_instanceIds.TryGetValue(parent, out var ids) || parent.Document is not { } key)
        {
            return [];
        }

        var existing = (parent.Children ?? []).Where(child => child.Kind != TreeRowKind.MorePage).ToList();
        var take = ids.Skip(existing.Count).Take(InstancePageSize).ToList();
        var names = await _runtime.Services.Source.GetInstanceNamesAsync(key, take, _token);
        var page = names.Select(name => new TreeRow(TreeRowKind.Instance, name.Name, parent,
            path: RowPath.ForElement(name.Id), id: name.Id)
        {
            Key = RowKey(TreeRowKind.Instance, key, element: name.Id)
        }).ToList();
        var shown = existing.Count + take.Count;
        if (shown < ids.Length)
        {
            page.Add(new TreeRow(TreeRowKind.MorePage,
                string.Format(CultureInfo.CurrentCulture, "{0:N0} of {1:N0} · more", shown, ids.Length), parent)
            {
                Key = parent.Key + "|" + RowKey(TreeRowKind.MorePage, key)
            });
        }

        parent.Children = existing.Concat(page).ToList();
        return page;
    }

    private DocumentScope? DocumentOf(TreeRow row)
    {
        return row.Kind is TreeRowKind.AllModelsHeader or TreeRowKind.AllModelsGroup || row.Document is not { } key
            ? null
            : _data.Find(key);
    }

    private long? GroupIdOf(TreeRow row)
    {
        for (var current = row; current is not null; current = current.Parent)
        {
            if (current.Kind == TreeRowKind.Group)
            {
                return current.Id;
            }
        }

        return null;
    }

    private async Task<bool> ReadHiddenAsync()
    {
        var changed = false;
        foreach (var document in _data.Documents.Where(document => document.Key.IsCurrentModel && document.Info.IsLoaded)
                     .ToList())
        {
            changed |= await _visibility.ReadBackAsync(document.Key, _token);
        }

        if (changed)
        {
            ComputeStates();
        }

        return changed;
    }

    private IEnumerable<long> VisibleIdsOf(DocumentKey document)
    {
        if (_data.Find(document) is not { } scope)
        {
            return [];
        }

        var visible = new List<TreeRow>();
        Flatten(_roots, false, visible);
        var snapshot = scope.Snapshot;
        var groups = new HashSet<int>();
        var ids = new List<long>();
        foreach (var row in visible)
        {
            switch (row.Kind)
            {
                case TreeRowKind.AllModelsGroup when snapshot.GroupIndexOf(row.Id) is var index and >= 0:
                    groups.Add(index);
                    break;
                case TreeRowKind.Group or TreeRowKind.Family or TreeRowKind.Type when row.Document == document:
                    groups.Add(row.Path.Group);
                    break;
                case TreeRowKind.Instance when row.Document == document:
                    ids.Add(row.Id);
                    break;
            }
        }

        if (groups.Count == 0)
        {
            return ids;
        }

        if (scope.Ids is null)
        {
            ids.AddRange(snapshot.Rows.Where(row => groups.Contains(row.GroupIndex)).Select(row => row.Id));
        }
        else
        {
            ids.AddRange(scope.Ids.Where(id => snapshot.TryGetRow(id, out var row) && groups.Contains(row.GroupIndex)));
        }

        return ids;
    }

    private void OnVisibilityChanged()
    {
        ComputeStates();
        Publish();
    }

    private void ComputeStates()
    {
        _states.Clear();
        foreach (var document in _data.Documents)
        {
            var hidden = _visibility.HiddenIds(document);
            _states[document.Key] = new DocumentState(hidden, ScopeCounter.Count(document.Snapshot, hidden));
        }

        ComputeSelected();
    }

    private void ComputeSelected()
    {
        foreach (var document in _data.Documents)
        {
            if (_states.TryGetValue(document.Key, out var state))
            {
                state.Selected = ScopeCounter.Count(document.Snapshot, _selectedElements
                    .Where(element => element.Document == document.Key)
                    .Select(element => element.Id));
            }
        }
    }

    private void Publish()
    {
        foreach (var root in _roots)
        {
            ApplyState(root);
        }

        var visible = new List<TreeRow>();
        Flatten(_roots, false, visible);
        Rows.ReplaceAll(visible);
    }

    private void ApplyState(TreeRow row)
    {
        if (DocumentOf(row) is { } document)
        {
            row.DisabledReason = document.Info.IsLoaded ? null : VisibilityReasons.NotLoaded;
        }

        var stale = _visibility.Journal.IsStale;
        switch (row.Kind)
        {
            case TreeRowKind.AllModelsHeader:
                row.Count = _data.Documents.Sum(document => document.Counts.Count);
                row.SelectedInside = _states.Values.Sum(state => state.Selected.Count);
                row.Eye = RowEye.None;
                break;
            case TreeRowKind.AllModelsGroup:
                ApplyAllModelsState(row);
                break;
            case TreeRowKind.CurrentModelHeader or TreeRowKind.LinkModel when DocumentOf(row) is { } scope:
                var state = _states[scope.Key];
                row.Count = scope.Counts.Count;
                row.SelectedInside = state.Selected.Count;
                row.Eye = row.IsHeader ? RowEye.None
                    : stale ? RowEye.Unknown
                    : EyeOf(state.Hidden.Count, scope.Snapshot.ElementCount);
                break;
            case TreeRowKind.Group or TreeRowKind.Family or TreeRowKind.Type when DocumentOf(row) is { } scope:
                var keys = KeyPath(row.Path, scope.Snapshot.GroupingDepth);
                var branch = _states[scope.Key];
                row.Count = scope.Counts.CountAt(keys);
                row.SelectedInside = branch.Selected.CountAt(keys);
                row.Eye = GroupIdOf(row) is { } group && _visibility.IsGroupHidden(group) ? RowEye.HiddenByAllModels
                    : stale && row.IsInsideLink ? RowEye.Unknown
                    : EyeOf(branch.Hidden.CountAt(keys), scope.Snapshot.Counts.CountAt(keys));
                break;
            case TreeRowKind.Instance when DocumentOf(row) is { } scope:
                var element = new ElementRef(scope.Key, row.Id);
                row.SelectedInside = _selectedElements.Contains(element) ? 1 : 0;
                row.Eye = GroupIdOf(row) is { } owner && _visibility.IsGroupHidden(owner) ? RowEye.HiddenByAllModels
                    : stale && row.IsInsideLink ? RowEye.Unknown
                    : _states[scope.Key].HiddenIds.Contains(row.Id) ? RowEye.Hidden
                    : RowEye.Shown;
                break;
            default:
                row.Eye = RowEye.None;
                break;
        }

        if (row.Children is { } children)
        {
            foreach (var child in children)
            {
                ApplyState(child);
            }
        }
    }

    private void ApplyAllModelsState(TreeRow row)
    {
        int count = 0, selected = 0, hidden = 0, total = 0;
        foreach (var document in _data.Documents)
        {
            var index = document.Snapshot.GroupIndexOf(row.Id);
            if (index < 0)
            {
                continue;
            }

            var state = _states[document.Key];
            count += document.Counts.CountAt(index);
            selected += state.Selected.CountAt(index);
            hidden += state.Hidden.CountAt(index);
            total += document.Snapshot.Counts.CountAt(index);
        }

        row.Count = count;
        row.SelectedInside = selected;
        row.Eye = _visibility.IsGroupHidden(row.Id) ? RowEye.Hidden : EyeOf(hidden, total);
    }

    private void Flatten(IEnumerable<TreeRow> rows, bool underMatch, List<TreeRow> into)
    {
        foreach (var row in rows)
        {
            if (_searchHits is not null && !underMatch && !_searchHits.Contains(row))
            {
                continue;
            }

            into.Add(row);
            if (row.IsExpanded && row.Children is { } children)
            {
                Flatten(children, underMatch || _searchMatches.Contains(row), into);
            }
        }
    }

    private bool IsUnderMatch(TreeRow row)
    {
        for (var current = row; current is not null; current = current.Parent)
        {
            if (_searchMatches.Contains(current))
            {
                return true;
            }
        }

        return false;
    }

    private async Task ApplySearchAsync()
    {
        await CollectSearchAsync();
        await ReadHiddenAsync();
        Publish();
    }

    private async Task CollectSearchAsync()
    {
        var text = _searchText.Trim();
        if (text.Length == 0)
        {
            _searchHits = null;
            _searchMatches = [];
            return;
        }

        var hits = new HashSet<TreeRow>();
        var matches = new HashSet<TreeRow>();
        foreach (var root in _roots)
        {
            await CollectHitsAsync(root, text, hits, matches);
        }

        foreach (var element in ParseToken(text))
        {
            for (var row = await RevealAsync(element); row is not null; row = row.Parent)
            {
                matches.Add(row);
                hits.Add(row);
            }
        }

        _searchHits = hits;
        _searchMatches = matches;
    }

    private async Task<bool> CollectHitsAsync(TreeRow row, string text, HashSet<TreeRow> hits, HashSet<TreeRow> matches)
    {
        var self = Matches(row, text);
        if (self)
        {
            matches.Add(row);
        }

        var depth = DocumentOf(row)?.Snapshot.GroupingDepth ?? 0;
        var browse = row.Kind is TreeRowKind.AllModelsHeader or TreeRowKind.CurrentModelHeader or TreeRowKind.LinkModel
            or TreeRowKind.Family || (row.Kind == TreeRowKind.Group && depth > 1);
        if (browse)
        {
            await EnsureChildrenAsync(row);
        }

        var any = false;
        foreach (var child in row.Children ?? [])
        {
            any |= await CollectHitsAsync(child, text, hits, matches);
        }

        if (any)
        {
            row.IsExpanded = true;
        }

        if (self || any)
        {
            hits.Add(row);
        }

        return self || any;
    }

    private IEnumerable<ElementRef> ParseToken(string text)
    {
        var match = IdToken.Match(text);
        if (!match.Success || !long.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture,
                out var id))
        {
            return [];
        }

        long? link = match.Groups[1].Success
            ? long.Parse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture)
            : null;
        return _data.Documents
            .Where(document => link is null || document.Key.LinkInstanceId == link)
            .Where(document => document.Snapshot.Contains(id))
            .Select(document => new ElementRef(document.Key, id))
            .ToList();
    }

    private sealed class DocumentState(HashSet<long> hiddenIds, CountNode hidden)
    {
        public HashSet<long> HiddenIds { get; } = hiddenIds;

        public CountNode Hidden { get; } = hidden;

        public CountNode Selected { get; set; } = new(-1);
    }
}
