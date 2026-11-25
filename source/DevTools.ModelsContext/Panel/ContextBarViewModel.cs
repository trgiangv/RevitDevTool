using CommunityToolkit.Mvvm.ComponentModel;
using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Scope;
using System.Collections.ObjectModel;

namespace DevTools.ModelsContext.Panel;

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
                _owner.OnRefused(value);
                return;
            }

            _isChecked = value;
            OnPropertyChanged();
            _owner.OnChecksChanged();
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

    public override string ToString()
    {
        return Label;
    }
}

public sealed partial class ContextBarViewModel : ObservableObject
{
    public const string CurrentModelLabel = "Current model";

    public const string LastDocumentHint = "Keep at least one document checked.";

    private int _batch;

    public event EventHandler? Changed;

    public event EventHandler<string>? Refused;

    public ObservableCollection<DocumentItemViewModel> Documents { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSelectionScope))]
    [NotifyPropertyChangedFor(nameof(IsActiveViewScope))]
    [NotifyPropertyChangedFor(nameof(IsWholeModelScope))]
    public partial ScopeKind Scope { get; set; } = ScopeKind.ActiveView;

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

    public DocumentKey? CurrentModel => Documents.FirstOrDefault(item => item.Key.IsCurrentModel)?.Key;

    public IReadOnlyList<DocumentKey> CheckedDocuments => Documents
        .Where(item => item.IsChecked)
        .Select(item => item.Key)
        .ToList();

    public string Summary => string.Join(", ", Documents.Where(item => item.IsChecked).Select(item => item.Label));

    public string LabelOf(DocumentKey key)
    {
        return Documents.FirstOrDefault(item => item.Key == key)?.Label
               ?? (key.IsCurrentModel ? CurrentModelLabel : key.ToString());
    }

    public void SetDocuments(IReadOnlyList<DocumentInfo> documents)
    {
        SetDocuments(documents, null);
    }

    internal void SetDocuments(IReadOnlyList<DocumentInfo> documents, IReadOnlyCollection<DocumentKey>? checks)
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

            if (checks is not null)
            {
                item.SetChecked(checks.Contains(info.Key));
            }

            Documents.Add(item);
        }

        if (!Documents.Any(item => item.IsChecked))
        {
            Documents.FirstOrDefault(item => item.IsEnabled)?.SetChecked(true);
        }

        _batch--;
        OnPropertyChanged(nameof(CurrentModel));
        OnChecksChanged();
    }

    public void Check(IEnumerable<DocumentKey> keys)
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
            OnChecksChanged();
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

    internal void OnRefused(bool value)
    {
        if (!value)
        {
            Refused?.Invoke(this, LastDocumentHint);
        }
    }

    internal void OnChecksChanged()
    {
        if (_batch > 0)
        {
            return;
        }

        OnPropertyChanged(nameof(Summary));
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

    partial void OnScopeChanged(ScopeKind value)
    {
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void SetScopeWhen(bool value, ScopeKind scope)
    {
        if (value)
        {
            Scope = scope;
        }
    }
}
