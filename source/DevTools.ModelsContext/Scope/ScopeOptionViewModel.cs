using CommunityToolkit.Mvvm.ComponentModel;

namespace DevTools.ModelsContext.Scope;

public sealed class ScopeOptionViewModel : ObservableObject
{
    private readonly Action<ScopeOptionViewModel, bool> _changed;
    private bool _isChecked;

    internal ScopeOptionViewModel(string key, string name, Action<ScopeOptionViewModel, bool> changed)
    {
        Key = key;
        Name = name;
        _changed = changed;
    }

    public string Name { get; }

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (value != _isChecked)
            {
                _changed(this, value);
            }
        }
    }

    public bool IsOutsideParent { get; private set; }

    public bool IsUnresolved { get; private set; }

    internal string Key { get; }

    internal void Set(bool isChecked, bool isOutsideParent, bool isUnresolved)
    {
        SetProperty(ref _isChecked, isChecked, nameof(IsChecked));
        if (IsOutsideParent != isOutsideParent)
        {
            IsOutsideParent = isOutsideParent;
            OnPropertyChanged(nameof(IsOutsideParent));
        }

        if (IsUnresolved != isUnresolved)
        {
            IsUnresolved = isUnresolved;
            OnPropertyChanged(nameof(IsUnresolved));
        }
    }

    internal void Init(bool isChecked, bool isOutsideParent, bool isUnresolved)
    {
        _isChecked = isChecked;
        IsOutsideParent = isOutsideParent;
        IsUnresolved = isUnresolved;
    }

    public override string ToString()
    {
        return Name;
    }
}
