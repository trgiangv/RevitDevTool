using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Parameters;
using DevTools.ModelsContext.Scope;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;

namespace DevTools.ModelsContext.Filters;

public sealed partial class RuleSetViewModel : FilterNodeViewModel
{
    public const string UnresolvedText = "Not in this model: {0}. This group matches nothing.";
    public const string OutsideText = "Outside the parent scope: {0}.";

    private readonly List<long> _categories = [];
    private readonly List<SourceId> _documents = [];
    private SetMatch _match;

    internal RuleSetViewModel(IFilterEditor editor, bool isReadOnly, SetMatch match = SetMatch.All,
        FilterScope? scope = null)
        : base(editor, isReadOnly)
    {
        _match = match;
        if (scope is not null)
        {
            _documents.AddRange(scope.Documents.Distinct());
            _categories.AddRange(scope.Categories.Distinct());
        }

        Children.CollectionChanged += OnChildrenChanged;
    }

    public ObservableCollection<FilterNodeViewModel> Children { get; } = [];

    public SetMatch Match
    {
        get => _match;
        set
        {
            if (IsReadOnly || !SetProperty(ref _match, value))
            {
                return;
            }

            OnPropertyChanged(nameof(MatchText));
            foreach (var child in Children)
            {
                child.NotifyConnector();
            }

            Edited();
        }
    }

    public string MatchText => EnumText.Of(Match);

    public bool IsRoot => Parent is null;

    [ObservableProperty]
    public partial bool IsExpanded { get; set; } = true;

    public ObservableCollection<ScopeOptionViewModel> DocumentOptions { get; } = [];

    public ObservableCollection<ScopeOptionViewModel> CategoryOptions { get; } = [];

    public IReadOnlyList<SourceId> ScopeDocuments => _documents;

    public IReadOnlyList<long> ScopeCategories => _categories;

    public bool HasScope => _documents.Count > 0 || _categories.Count > 0;

    [ObservableProperty]
    public partial EffectiveScope Effective { get; private set; } = EffectiveScope.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<ParameterInfo> Parameters { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWarning))]
    public partial string? Warning { get; private set; }

    public bool HasWarning => Warning is not null;

    [ObservableProperty]
    public partial bool IsUnresolved { get; private set; }

    internal static RuleSetViewModel From(FilterSet set, IFilterEditor editor, bool isReadOnly)
    {
        var model = new RuleSetViewModel(editor, isReadOnly, set.Match, set.Scope);
        foreach (var child in set.Children)
        {
            var node = From(child, editor, isReadOnly);
            node.Parent = model;
            model.Children.Add(node);
        }

        return model;
    }

    internal FilterSet ToSet()
    {
        return new FilterSet(Match, Children.Select(child => child.ToNode()).OfType<FilterNode>().ToList(),
            HasScope ? FilterScope.Of(_documents, _categories) : null);
    }

    internal override FilterNode? ToNode()
    {
        var set = ToSet();
        return set.Children.Count == 0 ? null : set;
    }

    public void SetDocumentScope(IEnumerable<SourceId> documents)
    {
        SetScope(_documents, documents.Distinct().ToList());
    }

    public void SetCategoryScope(IEnumerable<long> categories)
    {
        SetScope(_categories, categories.Distinct().ToList());
    }

    internal override void RefreshScope()
    {
        var parent = Parent?.Effective ?? Editor.RootScope;
        Effective = parent.Narrow(HasScope ? FilterScope.Of(_documents, _categories) : null);
        Parameters = Editor.Parameters.For(Effective);

        if (IsRoot)
        {
            DocumentOptions.Clear();
            CategoryOptions.Clear();
            Warning = null;
            IsUnresolved = false;
        }
        else
        {
            SyncOptions(parent);
        }

        foreach (var child in Children)
        {
            child.RefreshScope();
        }
    }

    internal void RemoveChild(FilterNodeViewModel node)
    {
        if (Children.Remove(node))
        {
            node.Parent = null;
            Edited();
        }
    }

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void ToggleMatch()
    {
        Match = Match == SetMatch.All ? SetMatch.Any : SetMatch.All;
    }

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void AddCondition()
    {
        Add(new RuleViewModel(Editor, false));
    }

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void AddGroup()
    {
        Add(new RuleSetViewModel(Editor, false));
    }

    private void Add(FilterNodeViewModel node)
    {
        node.Parent = this;
        Children.Add(node);
        node.RefreshScope();
        Edited();
    }

    private void OnChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var child in Children)
        {
            child.NotifyConnector();
        }

        if (e.OldItems is { } removed)
        {
            foreach (FilterNodeViewModel child in removed)
            {
                if (!Children.Contains(child))
                {
                    child.NotifyConnector();
                }
            }
        }
    }

    private void SetScope<T>(List<T> target, List<T> values)
    {
        if (IsReadOnly || target.SequenceEqual(values))
        {
            return;
        }

        target.Clear();
        target.AddRange(values);
        OnPropertyChanged(nameof(HasScope));
        OnPropertyChanged(nameof(ScopeDocuments));
        OnPropertyChanged(nameof(ScopeCategories));
        RefreshScope();
        Edited();
    }

    private void OnDocumentToggled(ScopeOptionViewModel option, bool value)
    {
        var source = new SourceId(option.Key);
        SetDocumentScope(value ? _documents.Append(source) : _documents.Where(item => item != source));
    }

    private void OnCategoryToggled(ScopeOptionViewModel option, bool value)
    {
        var id = long.Parse(option.Key, CultureInfo.InvariantCulture);
        SetCategoryScope(value ? _categories.Append(id) : _categories.Where(item => item != id));
    }

    private void SyncOptions(EffectiveScope parent)
    {
        var unresolved = _documents.Where(source => !Editor.IsKnown(source)).ToList();
        var outsideDocuments = _documents.Where(source => Editor.IsKnown(source) && !parent.Documents.Contains(source))
            .ToList();
        var documents = parent.Documents
            .Select(source => (Key: source.Value, Name: Editor.DocumentLabel(source), Outside: false, Unresolved: false))
            .Concat(outsideDocuments.Select(source => (source.Value, Editor.DocumentLabel(source), true, false)))
            .Concat(unresolved.Select(source => (source.Value, Editor.DocumentLabel(source), false, true)))
            .ToList();
        Sync(DocumentOptions, documents, key => _documents.Contains(new SourceId(key)), OnDocumentToggled);

        var available = parent.Categories ?? Editor.AllCategories;
        var outsideCategories = _categories.Where(id => !available.Contains(id)).ToList();
        var categories = available
            .Select(id => (Key: Key(id), Name: Editor.CategoryLabel(id), Outside: false, Unresolved: false))
            .Concat(outsideCategories.Select(id => (Key(id), Editor.CategoryLabel(id), true, false)))
            .ToList();
        Sync(CategoryOptions, categories, key => _categories.Contains(long.Parse(key,
            CultureInfo.InvariantCulture)), OnCategoryToggled);

        IsUnresolved = unresolved.Count > 0;
        var warnings = new List<string>();
        if (unresolved.Count > 0)
        {
            warnings.Add(string.Format(UnresolvedText, string.Join(", ", unresolved.Select(Editor.DocumentLabel))));
        }

        var outside = outsideDocuments.Select(Editor.DocumentLabel)
            .Concat(outsideCategories.Select(Editor.CategoryLabel))
            .ToList();
        if (outside.Count > 0)
        {
            warnings.Add(string.Format(OutsideText, string.Join(", ", outside)));
        }

        Warning = warnings.Count == 0 ? null : string.Join(" ", warnings);
    }

    private static string Key(long id)
    {
        return id.ToString(CultureInfo.InvariantCulture);
    }

    private static void Sync(
        ObservableCollection<ScopeOptionViewModel> options,
        List<(string Key, string Name, bool Outside, bool Unresolved)> wanted,
        Func<string, bool> isChecked,
        Action<ScopeOptionViewModel, bool> changed)
    {
        if (options.Select(option => option.Key).SequenceEqual(wanted.Select(item => item.Key)))
        {
            foreach (var option in options)
            {
                var item = wanted.First(entry => entry.Key == option.Key);
                option.Set(isChecked(option.Key), item.Outside, item.Unresolved);
            }

            return;
        }

        options.Clear();
        foreach (var item in wanted)
        {
            var option = new ScopeOptionViewModel(item.Key, item.Name, changed);
            option.Set(isChecked(item.Key), item.Outside, item.Unresolved);
            options.Add(option);
        }
    }
}
