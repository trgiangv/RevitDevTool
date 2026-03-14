using System.ComponentModel;
namespace RevitMcpToolSet.Data;

public class ScheduleSortRule
{
    [Description("Field name to sort by")] public string FieldName { get; set; } = "";
    [Description("Sort direction")] public ScheduleSortOrder Direction { get; set; }
    [Description("Sort priority order")] public int SortOrder { get; set; }
}

public class ScheduleGroupRule
{
    [Description("Field name to group by")] public string FieldName { get; set; } = "";
    public bool ShowHeader { get; set; } = true;
    public bool ShowFooter { get; set; }
    public bool ShowFooterTitle { get; set; }
    public bool ShowFooterCount { get; set; }
    public bool ShowBlankLine { get; set; }
}

public class ScheduleFilterRule
{
    [Description("Field name to filter on")] public string FieldName { get; set; } = "";
    [Description("Filter type: Equal, NotEqual, Contains, NotContains, GreaterThan, LessThan, GreaterThanOrEqual, LessThanOrEqual, BeginsWith, EndsWith, HasNoValue, HasValue")]
    public string FilterType { get; set; } = "";
    [Description("Filter value")] public string Value { get; set; } = "";
    [Description("Whether the value is numeric")] public bool IsNumeric { get; set; }
    [Description("Case sensitivity")] public bool IsCaseSensitive { get; set; }
}
