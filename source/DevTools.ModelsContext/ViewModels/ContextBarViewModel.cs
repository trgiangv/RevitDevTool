using CommunityToolkit.Mvvm.ComponentModel;
using DevTools.ModelsContext.Model;
using System.Collections.ObjectModel;

namespace DevTools.ModelsContext.ViewModels;

public sealed class DocumentItemViewModel : ObservableObject
{
    private readonly ContextBarViewModel _owner;
    private bool _isChecked;

    internal DocumentItemViewModel(ContextBarViewModel owner, DocumentInfo info, string label)
    {
        _owner = owner;
        Info = info;
        Label = label;
    }

    public DocumentInfo Info { get; private set; }

    public DocumentKey Key => Info.Key;

    public string Label { get; private set; }

    public bool IsEnabled => Info.IsLoaded;

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (value == _isChecked)
            {
                return;
            }

            if (!_owner.CanChange(this, value))
            {
                OnPropertyChanged();
                return;
            }

            _isChecked = value;
            OnPropertyChanged();
            _owner.OnTicksChanged();
        }
    }

    internal void Update(DocumentInfo info, string label)
    {
        Info = info;
        Label = label;
        OnPropertyChanged(string.Empty);
    }

    internal void SetChecked(bool value)
    {
        if (_isChecked == value)
        {
            return;
        }

        _isChecked = value;
        OnPropertyChanged(nameof(IsChecked));
    }
}

public sealed class ContextBarViewModel : ObservableObject
{
    public const string CurrentModelLabel = "Current model";

    private ScopeKind _scope = ScopeKind.ActiveView;
    private int _batch;

    public event EventHandler? Changed;

    public ObservableCollection<DocumentItemViewModel> Documents { get; } = [];

    public ScopeKind Scope
    {
        get => _scope;
        set
        {
            if (!SetProperty(ref _scope, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsSelectionScope));
            OnPropertyChanged(nameof(IsActiveViewScope));
            OnPropertyChanged(nameof(IsWholeModelScope));
            OnPropertyChanged(nameof(IsDocumentListVisible));
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool IsSelectionScope
    {
        get => Scope == ScopeKind.Selection;
        set => SetScopeWhen(value, ScopeKind.Selection);
    }

    public bool IsActiveViewScope
    {
        get => Scope == ScopeKind.ActiveView;
        set => SetScopeWhen(value, ScopeKind.ActiveView);
    }

    public bool IsWholeModelScope
    {
        get => Scope == ScopeKind.WholeModel;
        set => SetScopeWhen(value, ScopeKind.WholeModel);
    }

    public bool IsDocumentListVisible => Scope != ScopeKind.Selection;

    public DocumentKey? CurrentModel => Documents.FirstOrDefault(item => item.Key.IsCurrentModel)?.Key;

    public IReadOnlyList<DocumentKey> TickedDocuments => Documents
        .Where(item => item.IsChecked)
        .Select(item => item.Key)
        .ToList();

    public string Summary => string.Join(", ", Documents.Where(item => item.IsChecked).Select(item => item.Label));

    public string SummaryToolTip => string.Join(Environment.NewLine,
        Documents.Where(item => item.IsChecked).Select(item => item.Label));

    public string LabelOf(DocumentKey key)
    {
        return Documents.FirstOrDefault(item => item.Key == key)?.Label
               ?? (key.IsCurrentModel ? CurrentModelLabel : key.ToString());
    }

    public void SetDocuments(IReadOnlyList<DocumentInfo> documents)
    {
        SetDocuments(documents, null);
    }

    internal void SetDocuments(IReadOnlyList<DocumentInfo> documents, IReadOnlyCollection<DocumentKey>? ticks)
    {
        var labels = Labels(documents);
        var known = Documents.ToDictionary(item => item.Key);
        var first = Documents.Count == 0;
        _batch++;
        Documents.Clear();
        foreach (var info in documents.OrderBy(info => !info.Key.IsCurrentModel))
        {
            if (known.TryGetValue(info.Key, out var item))
            {
                item.Update(info, labels[info.Key]);
            }
            else
            {
                item = new DocumentItemViewModel(this, info, labels[info.Key]);
                item.SetChecked(first && info.Key.IsCurrentModel && info.IsLoaded);
            }

            if (ticks is not null)
            {
                item.SetChecked(ticks.Contains(info.Key));
            }

            Documents.Add(item);
        }

        if (!Documents.Any(item => item.IsChecked && item.IsEnabled))
        {
            (Documents.FirstOrDefault(item => item.Key.IsCurrentModel && item.IsEnabled)
             ?? Documents.FirstOrDefault(item => item.IsEnabled))?.SetChecked(true);
        }

        _batch--;
        OnPropertyChanged(nameof(CurrentModel));
        OnTicksChanged();
    }

    public void Tick(IEnumerable<DocumentKey> keys)
    {
        var wanted = keys.ToHashSet();
        var changed = false;
        foreach (var item in Documents.Where(item => item.IsEnabled && wanted.Contains(item.Key) && !item.IsChecked))
        {
            item.SetChecked(true);
            changed = true;
        }

        if (changed)
        {
            OnTicksChanged();
        }
    }

    internal bool CanChange(DocumentItemViewModel item, bool value)
    {
        if (value)
        {
            return item.IsEnabled;
        }

        return Documents.Any(other => !ReferenceEquals(other, item) && other.IsChecked && other.IsEnabled);
    }

    internal void OnTicksChanged()
    {
        if (_batch > 0)
        {
            return;
        }

        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(SummaryToolTip));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static Dictionary<DocumentKey, string> Labels(IReadOnlyList<DocumentInfo> documents)
    {
        var labels = new Dictionary<DocumentKey, string>();
        foreach (var group in documents.GroupBy(info => info.Key.IsCurrentModel ? CurrentModelLabel : info.DisplayName,
                     StringComparer.OrdinalIgnoreCase))
        {
            var index = 0;
            foreach (var info in group.OrderBy(info => info.Key.LinkInstanceId))
            {
                labels[info.Key] = index == 0
                    ? group.Key
                    : $"{group.Key} ({index + 1})";
                index++;
            }
        }

        return labels;
    }

    private void SetScopeWhen(bool value, ScopeKind scope)
    {
        if (value)
        {
            Scope = scope;
        }
    }
}
