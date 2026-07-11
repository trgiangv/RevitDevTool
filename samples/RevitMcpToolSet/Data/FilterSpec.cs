using System.ComponentModel;
using System.Text.Json.Serialization;

namespace RevitMcpToolSet.Data;

/// <summary>
/// Filter type discriminators for <see cref="FilterItem"/>.
/// </summary>
public static class FilterTypes
{
    public const string Category = "category";
    public const string ParameterString = "parameter_string";
    public const string ParameterNumeric = "parameter_numeric";
    public const string ParameterHasValue = "parameter_has_value";
    public const string Level = "level";
    public const string Class = "class";
    public const string BoundingBox = "bounding_box";
    public const string View = "view";
    public const string ElementType = "element_type";
    public const string Workset = "workset";
    public const string Phase = "phase";
    public const string Exclusion = "exclusion";
}

/// <summary>
/// String comparison operators for <see cref="FilterTypes.ParameterString"/> filters.
/// </summary>
public static class StringOperators
{
    public const string Equal = "equals";
    public const string NotEqual = "not_equals";
    public const string Contains = "contains";
    public const string NotContains = "not_contains";
    public const string BeginsWith = "begins_with";
    public const string EndsWith = "ends_with";
}

/// <summary>
/// Numeric comparison operators for <see cref="FilterTypes.ParameterNumeric"/> filters.
/// </summary>
public static class NumericOperators
{
    public const string Equal = "equals";
    public const string NotEqual = "not_equals";
    public const string GreaterThan = "greater_than";
    public const string LessThan = "less_than";
    public const string GreaterOrEqual = "greater_or_equal";
    public const string LessOrEqual = "less_or_equal";
}

/// <summary>
/// Bounding-box spatial modes for <see cref="FilterTypes.BoundingBox"/> filters.
/// </summary>
public static class BoundingBoxModes
{
    public const string Inside = "inside";
    public const string Intersecting = "intersecting";
}

/// <summary>
/// Single filter clause in the 12-type discriminated union. Set <see cref="Type"/> then populate
/// the fields relevant to that type.
/// </summary>
public class FilterItem
{
    [Description(
        "Filter discriminator: category, parameter_string, parameter_numeric, parameter_has_value, " +
        "level, class, bounding_box, view, element_type, workset, phase, or exclusion.")]
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    // --- category ---
    [Description("Category display names (type=category), e.g. ['Walls', 'Doors'].")]
    [JsonPropertyName("names")]
    public string[]? Names { get; set; }

    [Description("When true, exclude elements in the listed categories instead of including them (type=category).")]
    [JsonPropertyName("inverted")]
    public bool Inverted { get; set; }

    // --- parameter_string | parameter_numeric | parameter_has_value ---
    [Description("Parameter name as shown in Revit Properties (type=parameter_*).")]
    [JsonPropertyName("parameter_name")]
    public string? ParameterName { get; set; }

    [Description(
        "Comparison operator. String: equals, not_equals, contains, not_contains, begins_with, ends_with. " +
        "Numeric: equals, not_equals, greater_than, less_than, greater_or_equal, less_or_equal.")]
    [JsonPropertyName("operator")]
    public string? Operator { get; set; }

    [Description("Filter value — string for parameter_string, number for parameter_numeric.")]
    [JsonPropertyName("value")]
    public object? Value { get; set; }

    [Description("True = parameter has a value; false = parameter is empty (type=parameter_has_value).")]
    [JsonPropertyName("has_value")]
    public bool? HasValue { get; set; }

    // --- level ---
    [Description("Exact level name, e.g. 'Level 1' (type=level).")]
    [JsonPropertyName("level_name")]
    public string? LevelName { get; set; }

    // --- class ---
    [Description("Revit API class names, e.g. ['Wall', 'FamilyInstance', 'Room'] (type=class).")]
    [JsonPropertyName("class_names")]
    public string[]? ClassNames { get; set; }

    // --- bounding_box (all coordinates in feet — Revit internal units) ---
    [Description("[x, y, z] minimum corner in feet (type=bounding_box).")]
    [JsonPropertyName("min_point")]
    public double[]? MinPoint { get; set; }

    [Description("[x, y, z] maximum corner in feet (type=bounding_box).")]
    [JsonPropertyName("max_point")]
    public double[]? MaxPoint { get; set; }

    [Description("Spatial mode: inside (default) or intersecting (type=bounding_box).")]
    [JsonPropertyName("mode")]
    public string? Mode { get; set; }

    // --- view ---
    [Description("View name to scope to; null = active view (type=view).")]
    [JsonPropertyName("view_name")]
    public string? ViewName { get; set; }

    // --- element_type ---
    [Description("True = element types only; false = instances only (type=element_type).")]
    [JsonPropertyName("is_type")]
    public bool? IsType { get; set; }

    // --- workset ---
    [Description("Exact workset name (type=workset).")]
    [JsonPropertyName("workset_name")]
    public string? WorksetName { get; set; }

    // --- phase ---
    [Description("Exact phase name, e.g. 'New Construction' (type=phase).")]
    [JsonPropertyName("phase_name")]
    public string? PhaseName { get; set; }

    // --- exclusion ---
    [Description("Element IDs to exclude (type=exclusion).")]
    [JsonPropertyName("element_ids")]
    public long[]? ElementIds { get; set; }
}

/// <summary>
/// Composable element query specification with AND/OR filter logic.
/// </summary>
public class FilterSpec
{
    [Description("List of filter clauses combined per <see cref=\"Logic\"/>.")]
    [JsonPropertyName("filters")]
    public FilterItem[] Filters { get; set; } = [];

    [Description("How to combine filters: 'and' (all must match) or 'or' (any must match).")]
    [JsonPropertyName("logic")]
    public string Logic { get; set; } = "and";
}
