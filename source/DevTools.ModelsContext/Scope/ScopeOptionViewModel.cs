using CommunityToolkit.Mvvm.ComponentModel;

namespace DevTools.ModelsContext.Scope;

public sealed partial class ScopeOptionViewModel : ObservableObject
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

    [ObservableProperty]
    public partial bool IsOutsideParent { get; private set; }

    [ObservableProperty]
    public partial bool IsUnresolved { get; private set; }

    internal string Key { get; }

    internal void Set(bool isChecked, bool isOutsideParent, bool isUnresolved)
    {
        SetProperty(ref _isChecked, isChecked, nameof(IsChecked));
        IsOutsideParent = isOutsideParent;
        IsUnresolved = isUnresolved;
    }

    public override string ToString()
    {
        return Name;
    }
}
