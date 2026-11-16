using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTools.ModelsContext.Model;
using System.Collections.ObjectModel;

namespace DevTools.ModelsContext.ViewModels;

internal interface IFilterEditor
{
    IReadOnlyList<ParameterInfo> Parameters { get; }

    ParameterInfo? FindParameter(ParameterKey key);

    void OnEdited();

    Task LoadValuesAsync(RuleViewModel rule);
}

public abstract partial class FilterNodeViewModel : ObservableObject
{
    private protected FilterNodeViewModel(IFilterEditor editor, bool isReadOnly)
    {
        Editor = editor;
        IsReadOnly = isReadOnly;
    }

    public bool IsReadOnly { get; }

    internal IFilterEditor Editor { get; }

    internal RuleSetViewModel? Parent { get; set; }

    private protected bool CanEdit => !IsReadOnly;

    private bool CanRemove => Parent is { IsReadOnly: false };

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

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private void Remove()
    {
        Parent?.RemoveChild(this);
    }
}

public sealed partial class RuleSetViewModel : FilterNodeViewModel
{
    private bool _isInverted;
    private SetMatch _match;

    internal RuleSetViewModel(IFilterEditor editor, bool isReadOnly, SetMatch match = SetMatch.All,
        bool isInverted = false)
        : base(editor, isReadOnly)
    {
        _match = match;
        _isInverted = isInverted;
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

    public bool IsInverted
    {
        get => _isInverted;
        set
        {
            if (!IsReadOnly && SetProperty(ref _isInverted, value))
            {
                Edited();
            }
        }
    }

    internal static RuleSetViewModel From(FilterSet set, IFilterEditor editor, bool isReadOnly)
    {
        var model = new RuleSetViewModel(editor, isReadOnly, set.Match, set.Inverted);
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
        return new FilterSet(Match, Children.Select(child => child.ToNode()).OfType<FilterNode>().ToList(), IsInverted);
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

    internal void RemoveChild(FilterNodeViewModel node)
    {
        if (Children.Remove(node))
        {
            node.Parent = null;
            Edited();
        }
    }

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void AddRule()
    {
        Add(new RuleViewModel(Editor, false));
    }

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void AddSet()
    {
        Add(new RuleSetViewModel(Editor, false));
    }

    private void Add(FilterNodeViewModel node)
    {
        node.Parent = this;
        Children.Add(node);
        Edited();
    }
}

public sealed partial class RuleViewModel : FilterNodeViewModel
{
    private bool _isCaseSensitive;
    private FilterOperator _operator;
    private ParameterKey? _parameter;
    private string _parameterName;
    private bool _refreshing;
    private ParameterInfo? _selectedParameter;
    private string _value;

    internal RuleViewModel(IFilterEditor editor, bool isReadOnly, FilterRule? rule = null)
        : base(editor, isReadOnly)
    {
        _parameter = rule?.Parameter;
        _parameterName = rule?.Parameter.Name ?? string.Empty;
        _operator = rule?.Operator ?? FilterOperator.Equal;
        _value = rule?.Value ?? string.Empty;
        _isCaseSensitive = rule?.IsCaseSensitive ?? false;
        _selectedParameter = rule is null ? null : editor.FindParameter(rule.Parameter);
    }

    public IReadOnlyList<ParameterInfo> Parameters => Editor.Parameters;

    public ParameterInfo? SelectedParameter
    {
        get => _selectedParameter;
        set
        {
            if (IsReadOnly || _refreshing || Equals(value, _selectedParameter))
            {
                return;
            }

            _parameter = value?.Key;
            _parameterName = value?.DisplayName ?? string.Empty;
            OnPropertyChanged(nameof(ParameterName));
            Select(value);
            Values = [];
            Edited();
        }
    }

    public string ParameterName
    {
        get => _parameterName;
        set
        {
            if (IsReadOnly || _refreshing || !SetProperty(ref _parameterName, value ?? string.Empty))
            {
                return;
            }

            var match = Parameters.FirstOrDefault(parameter =>
                string.Equals(parameter.DisplayName, _parameterName.Trim(), StringComparison.OrdinalIgnoreCase));
            _parameter = match?.Key;
            Select(match);
            Values = [];
            Edited();
        }
    }

    public ValueKind Kind => _selectedParameter?.Kind ?? ValueKind.Text;

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

    public bool CanBeCaseSensitive => Kind == ValueKind.Text;

    public bool IsCaseSensitive
    {
        get => _isCaseSensitive;
        set
        {
            if (!IsReadOnly && SetProperty(ref _isCaseSensitive, value))
            {
                Edited();
            }
        }
    }

    [ObservableProperty]
    public partial IReadOnlyList<ValueCount> Values { get; private set; } = [];

    [ObservableProperty]
    public partial bool HasMoreValues { get; private set; }

    [ObservableProperty]
    public partial int NoParameterCount { get; private set; }

    public ParameterKey? Parameter => _parameterName.Trim().Length == 0
        ? null
        : _parameter ?? new ParameterKey(_parameterName.Trim());

    internal override void NotifyParameters()
    {
        var kind = Kind;
        if (_parameter is { } key)
        {
            _selectedParameter = Editor.FindParameter(key) ?? _selectedParameter;
        }

        _refreshing = true;
        try
        {
            OnPropertyChanged(nameof(Parameters));
            OnPropertyChanged(nameof(SelectedParameter));
            OnPropertyChanged(nameof(ParameterName));
        }
        finally
        {
            _refreshing = false;
        }

        if (kind != Kind)
        {
            NotifyKind();
        }
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

        return new FilterRule(parameter, Operator, Value, CanBeCaseSensitive && IsCaseSensitive);
    }

    [RelayCommand]
    private Task LoadValuesAsync()
    {
        return Editor.LoadValuesAsync(this);
    }

    private void Select(ParameterInfo? parameter)
    {
        var kind = Kind;
        _selectedParameter = parameter;
        OnPropertyChanged(nameof(SelectedParameter));
        if (kind == Kind)
        {
            return;
        }

        NotifyKind();
        if (!FilterOperators.Allows(Kind, _operator))
        {
            _operator = Operators[0];
            OnPropertyChanged(nameof(Operator));
            OnPropertyChanged(nameof(NeedsValue));
        }
    }

    private void NotifyKind()
    {
        OnPropertyChanged(nameof(Kind));
        OnPropertyChanged(nameof(Operators));
        OnPropertyChanged(nameof(CanBeCaseSensitive));
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
