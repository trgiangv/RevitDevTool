using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTools.ModelsContext.Model;

namespace DevTools.ModelsContext.ViewModels;

public sealed partial class RuleViewModel : FilterNodeViewModel
{
    public const string TypeText = "Type";

    private bool _isCaseSensitive;
    private FilterOperator _operator;
    private ParameterKey? _parameter;
    private string? _exampleSpec;
    private string _parameterName;
    private NumberRange? _picked;
    private string? _pickedText;
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
        _picked = rule?.Picked;
        _pickedText = rule?.Picked is null ? null : rule.Value;
        _selectedParameter = rule is null ? null : Find(rule.Parameter);
    }

    public IReadOnlyList<ParameterInfo> Parameters => Parent?.Parameters ?? [];

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
            if (IsReadOnly || !SetProperty(ref _value, value ?? string.Empty))
            {
                return;
            }

            if (_picked is not null && !string.Equals(_value, _pickedText, StringComparison.Ordinal))
            {
                _picked = null;
            }

            Edited();
        }
    }

    public ValueCount? PickedValue
    {
        get => null;
        set
        {
            if (IsReadOnly || value is not { } item)
            {
                return;
            }

            var picked = item.Stored;
            var changed = picked != _picked;
            _picked = picked;
            _pickedText = item.Value;
            if (!string.Equals(_value, item.Value, StringComparison.Ordinal))
            {
                Value = item.Value;
                return;
            }

            if (changed)
            {
                Edited();
            }
        }
    }

    public NumberRange? Picked => _picked;

    [ObservableProperty]
    public partial string? ValueHint { get; private set; }

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

    internal override void RefreshScope()
    {
        var kind = Kind;
        if (_parameter is { } key)
        {
            _selectedParameter = Find(key) ?? _selectedParameter;
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

        LoadExample();
    }

    private void LoadExample()
    {
        var spec = _selectedParameter?.IsMeasured == true ? _selectedParameter.Spec : null;
        if (spec == _exampleSpec)
        {
            return;
        }

        _exampleSpec = spec;
        if (spec is null)
        {
            ValueHint = null;
            return;
        }

        _ = Editor.LoadExampleAsync(this);
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

        return new FilterRule(parameter, Operator, Value, CanBeCaseSensitive && IsCaseSensitive,
            Kind == ValueKind.Number ? _picked : null);
    }

    internal void SetExample(string? example)
    {
        ValueHint = example;
    }

    [RelayCommand]
    private Task LoadValuesAsync()
    {
        return Editor.LoadValuesAsync(this);
    }

    private ParameterInfo? Find(ParameterKey key)
    {
        return Editor.Parameters.Find(key, Editor.RootScope);
    }

    private void Select(ParameterInfo? parameter)
    {
        var kind = Kind;
        if (!Equals(parameter?.Key, _selectedParameter?.Key))
        {
            _picked = null;
            _pickedText = null;
        }

        _selectedParameter = parameter;
        OnPropertyChanged(nameof(SelectedParameter));
        LoadExample();
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
