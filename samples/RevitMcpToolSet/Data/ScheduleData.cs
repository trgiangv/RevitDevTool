using System.ComponentModel;
using System.Text.Json.Serialization;

namespace RevitMcpToolSet.Data;

/// <summary>
/// Full schedule creation configuration for <c>revit_create_schedule</c>.
/// </summary>
public class ScheduleConfig
{
    [Description("Category display name, e.g. 'Doors'.")]
    [JsonPropertyName("categoryName")]
    public string CategoryName { get; set; } = "";

    [Description("Name for the new schedule view.")]
    [JsonPropertyName("scheduleName")]
    public string ScheduleName { get; set; } = "";

    [Description("Field names to include in the schedule.")]
    public string[] Fields { get; set; } = [];

    [Description("Sort rules applied in order.")]
    [JsonPropertyName("sortRules")]
    public ScheduleSortRule[] SortRules { get; set; } = [];

    [Description("Filter rules applied to schedule rows.")]
    [JsonPropertyName("filterRules")]
    public ScheduleFilterRule[] FilterRules { get; set; } = [];

    [Description("Group rules for schedule layout.")]
    [JsonPropertyName("groupRules")]
    public ScheduleGroupRule[] GroupRules { get; set; } = [];
}

public class ScheduleSortRule
{
    [Description("Field name to sort by.")]
    [JsonPropertyName("field")]
    public string Field { get; set; } = "";

    [Description("Sort ascending when true, descending when false.")]
    [JsonPropertyName("ascending")]
    public bool Ascending { get; set; } = true;

    [JsonIgnore]
    public string FieldName
    {
        get => Field;
        set => Field = value;
    }

    [JsonIgnore]
    public ScheduleSortOrder Direction
    {
        get => Ascending ? ScheduleSortOrder.Ascending : ScheduleSortOrder.Descending;
        set => Ascending = value == ScheduleSortOrder.Ascending;
    }

    [Description("Sort priority order (legacy — lower values sort first).")]
    [JsonIgnore]
    public int SortOrder { get; set; }
}

public class ScheduleGroupRule
{
    [Description("Field name to group by.")]
    [JsonPropertyName("field")]
    public string Field { get; set; } = "";

    [Description("Show group header row.")]
    [JsonPropertyName("showHeader")]
    public bool ShowHeader { get; set; } = true;

    [Description("Show group footer row.")]
    [JsonPropertyName("showFooter")]
    public bool ShowFooter { get; set; }

    [JsonIgnore]
    public string FieldName
    {
        get => Field;
        set => Field = value;
    }

    [Description("Show footer title (legacy).")]
    [JsonIgnore]
    public bool ShowFooterTitle { get; set; }

    [Description("Show footer count (legacy).")]
    [JsonIgnore]
    public bool ShowFooterCount { get; set; }

    [Description("Show blank line after group (legacy).")]
    [JsonIgnore]
    public bool ShowBlankLine { get; set; }
}

public class ScheduleFilterRule
{
    [Description("Field name to filter on.")]
    [JsonPropertyName("field")]
    public string Field { get; set; } = "";

    [Description(
        "Filter operator: equals, not_equals, contains, not_contains, greater_than, less_than, " +
        "greater_or_equal, less_or_equal, begins_with, ends_with.")]
    [JsonPropertyName("operator")]
    public string Operator { get; set; } = "";

    [Description("Filter comparison value.")]
    [JsonPropertyName("value")]
    public string Value { get; set; } = "";

    [Description("Whether the value should be interpreted as numeric.")]
    [JsonPropertyName("isNumeric")]
    public bool IsNumeric { get; set; }

    [JsonIgnore]
    public string FieldName
    {
        get => Field;
        set => Field = value;
    }

    [JsonIgnore]
    public string FilterType
    {
        get => Operator;
        set => Operator = value;
    }

    [Description("Case sensitivity (legacy).")]
    [JsonPropertyName("isCaseSensitive")]
    public bool IsCaseSensitive { get; set; }
}
