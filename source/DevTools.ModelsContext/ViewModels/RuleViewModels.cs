using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTools.ModelsContext.Model;
using System.Collections.ObjectModel;

namespace DevTools.ModelsContext.ViewModels;

internal interface IFilterEditor
{
    IReadOnlyList<ParameterInfo> Parameters { get; }

    void OnEdited();

    Task LoadValuesAsync(RuleViewModel rule);
}

public abstract class FilterNodeViewModel : ObservableObject
{
    private protected FilterNodeViewModel(IFilterEditor editor, bool isReadOnly)
    {
        Editor = editor;
        IsReadOnly = isReadOnly;
    }

    public bool IsReadOnly { get; }

    internal IFilterEditor Editor { get; }

    internal abstract FilterNode? ToNode();

    internal virtual void NotifyParameters()
    {
    }

    internal static FilterNodeViewModel From(FilterNode node, IFilterEditor editor, bool isReadOnly)
    {
        return node switch
        {
            FilterSet set => RuleSetViewModel.From(set, editor, isReadOnly),
            FilterRule rule => new RuleViewModel(editor, isReadOnly, rule),
            UnsupportedRule unsupported => new UnsupportedRuleViewModel(editor, unsupported.Description),
            _ => new UnsupportedRuleViewModel(editor, node.ToString() ?? string.Empty)
        };
    }

    private protected void Edited()
    {
        Editor.OnEdited();
    }
}

public sealed class RuleSetViewModel : FilterNodeViewModel
{
    private SetMatch _match;

    internal RuleSetViewModel(IFilterEditor editor, bool isReadOnly, SetMatch match = SetMatch.All)
        : base(editor, isReadOnly)
    {
        _match = match;
        AddRuleCommand = new RelayCommand(() => Add(new RuleViewModel(Editor, false)), () => !IsReadOnly);
        AddSetCommand = new RelayCommand(() => Add(new RuleSetViewModel(Editor, false)), () => !IsReadOnly);
        RemoveCommand = new RelayCommand<FilterNodeViewModel?>(Remove, _ => !IsReadOnly);
    }

    public ObservableCollection<FilterNodeViewModel> Children { get; } = [];

    public SetMatch Match
    {
        get => _match;
        set
        {
            if (!IsReadOnly && SetProperty(ref _match, value))
            {
                Edited();
            }
        }
    }

    public IRelayCommand AddRuleCommand { get; }

    public IRelayCommand AddSetCommand { get; }

    public IRelayCommand<FilterNodeViewModel?> RemoveCommand { get; }

    internal static RuleSetViewModel From(FilterSet set, IFilterEditor editor, bool isReadOnly)
    {
        var model = new RuleSetViewModel(editor, isReadOnly, set.Match);
        foreach (var child in set.Children)
        {
            model.Children.Add(From(child, editor, isReadOnly));
        }

        return model;
    }

    internal FilterSet ToSet()
    {
        return new FilterSet(Match, Children.Select(child => child.ToNode()).OfType<FilterNode>().ToList());
    }

    internal override FilterNode? ToNode()
    {
        var set = ToSet();
        return set.Children.Count == 0 ? null : set;
    }

    internal override void NotifyParameters()
    {
        foreach (var child in Children)
        {
            child.NotifyParameters();
        }
    }

    private void Add(FilterNodeViewModel node)
    {
        Children.Add(node);
        Edited();
    }

    private void Remove(FilterNodeViewModel? node)
    {
        if (node is not null && Children.Remove(node))
        {
            Edited();
        }
    }
}

public sealed class RuleViewModel : FilterNodeViewModel
{
    private ValueKind _kind;
    private bool _hasMoreValues;
    private int _noParameterCount;
    private FilterOperator _operator;
    private ParameterKey? _parameter;
    private string _parameterName;
    private ParameterInfo? _selectedParameter;
    private string _value;
    private IReadOnlyList<ValueCount> _values = [];

    internal RuleViewModel(IFilterEditor editor, bool isReadOnly, FilterRule? rule = null)
        : base(editor, isReadOnly)
    {
        _parameter = rule?.Parameter;
        _parameterName = rule?.Parameter.Name ?? string.Empty;
        _kind = rule?.Kind ?? ValueKind.Text;
        _operator = rule?.Operator ?? FilterOperator.Equal;
        _value = rule?.Value ?? string.Empty;
        _selectedParameter = rule is null
            ? null
            : editor.Parameters.FirstOrDefault(parameter => parameter.Key == rule.Parameter);
        LoadValuesCommand = new AsyncRelayCommand(() => Editor.LoadValuesAsync(this));
    }

    public IReadOnlyList<ParameterInfo> Parameters => Editor.Parameters;

    public ParameterInfo? SelectedParameter
    {
        get => _selectedParameter;
        set
        {
            if (IsReadOnly || Equals(value, _selectedParameter))
            {
                return;
            }

            _selectedParameter = value;
            OnPropertyChanged();
            _parameter = value?.Key;
            _parameterName = value?.DisplayName ?? string.Empty;
            OnPropertyChanged(nameof(ParameterName));
            if (value is not null)
            {
                SetKind(value.Kind);
            }

            Values = [];
            Edited();
        }
    }

    public string ParameterName
    {
        get => _parameterName;
        set
        {
            if (IsReadOnly || !SetProperty(ref _parameterName, value ?? string.Empty))
            {
                return;
            }

            var match = Parameters.FirstOrDefault(parameter =>
                string.Equals(parameter.DisplayName, _parameterName.Trim(), StringComparison.OrdinalIgnoreCase));
            _selectedParameter = match;
            OnPropertyChanged(nameof(SelectedParameter));
            _parameter = match?.Key;
            if (match is not null)
            {
                SetKind(match.Kind);
            }

            Values = [];
            Edited();
        }
    }

    public ValueKind Kind
    {
        get => _kind;
        set
        {
            if (IsReadOnly || !SetKind(value))
            {
                return;
            }

            Edited();
        }
    }

    public IReadOnlyList<FilterOperator> Operators => FilterOperators.For(Kind);

    public FilterOperator Operator
    {
        get => _operator;
        set
        {
            if (IsReadOnly || !FilterOperators.Allows(Kind, value) || !SetProperty(ref _operator, value))
            {
                return;
            }

            OnPropertyChanged(nameof(NeedsValue));
            Edited();
        }
    }

    public bool NeedsValue => FilterOperators.NeedsValue(Operator);

    public string Value
    {
        get => _value;
        set
        {
            if (!IsReadOnly && SetProperty(ref _value, value ?? string.Empty))
            {
                Edited();
            }
        }
    }

    public IReadOnlyList<ValueCount> Values
    {
        get => _values;
        private set => SetProperty(ref _values, value);
    }

    public bool HasMoreValues
    {
        get => _hasMoreValues;
        private set => SetProperty(ref _hasMoreValues, value);
    }

    public int NoParameterCount
    {
        get => _noParameterCount;
        private set => SetProperty(ref _noParameterCount, value);
    }

    public IAsyncRelayCommand LoadValuesCommand { get; }

    public ParameterKey? Parameter => _parameterName.Trim().Length == 0
        ? null
        : _parameter ?? new ParameterKey(_parameterName.Trim());

    internal override void NotifyParameters()
    {
        if (_parameter is { } key && _selectedParameter is null)
        {
            _selectedParameter = Parameters.FirstOrDefault(parameter => parameter.Key == key);
            OnPropertyChanged(nameof(SelectedParameter));
        }

        OnPropertyChanged(nameof(Parameters));
    }

    internal void SetValues(IReadOnlyList<ValueCount> values, bool hasMore, int noParameter)
    {
        Values = values;
        HasMoreValues = hasMore;
        NoParameterCount = noParameter;
    }

    internal override FilterNode? ToNode()
    {
        if (Parameter is not { } parameter || (NeedsValue && Value.Length == 0))
        {
            return null;
        }

        return new FilterRule(parameter, Kind, Operator, Value);
    }

    private bool SetKind(ValueKind kind)
    {
        if (!SetProperty(ref _kind, kind, nameof(Kind)))
        {
            return false;
        }

        OnPropertyChanged(nameof(Operators));
        if (!FilterOperators.Allows(kind, _operator))
        {
            _operator = Operators[0];
            OnPropertyChanged(nameof(Operator));
            OnPropertyChanged(nameof(NeedsValue));
        }

        return true;
    }
}

public sealed class UnsupportedRuleViewModel : FilterNodeViewModel
{
    internal UnsupportedRuleViewModel(IFilterEditor editor, string description)
        : base(editor, true)
    {
        Description = description;
    }

    public string Description { get; }

    internal override FilterNode ToNode()
    {
        return new UnsupportedRule(Description);
    }
}
