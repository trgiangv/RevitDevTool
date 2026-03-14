using System.ComponentModel;
namespace RevitMcpToolSet.Data;

public enum MatchOperator
{
    Equals,
    Contains,
    StartsWith,
    EndsWith,
    GreaterThan,
    LessThan,
}

public class ParameterUpdate
{
    [Description("Parameter name")] public string ParameterName { get; set; } = "";
    [Description("New value")] public string Value { get; set; } = "";
}

public class ParameterCondition
{
    [Description("Parameter name to filter")] public string ParameterName { get; set; } = "";
    [Description("Comparison type")] public MatchOperator ComparisonType { get; set; }
    [Description("Value to compare")] public string Value { get; set; } = "";
}
