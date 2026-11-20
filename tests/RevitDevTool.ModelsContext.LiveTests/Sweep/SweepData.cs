using System.Globalization;

namespace RevitDevTool.ModelsContext.LiveTests;

internal sealed class SweepSetup
{
    public int EvaluationLimit { get; set; } = 6000;

    public int ScheduleLimit { get; set; } = 60;

    public int MaxDepth { get; set; } = 5;

    public int MaxRules { get; set; } = 50;

    public List<int> Seeds { get; set; } = [];
}

internal sealed class SweepRow
{
    public string Document { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Parameter { get; set; } = string.Empty;

    public string? BuiltIn { get; set; }

    public string Kind { get; set; } = string.Empty;

    public string Operator { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public int Oracle { get; set; }

    public int Panel { get; set; }

    public int? Native { get; set; }

    public int? ViewFilter { get; set; }

    public string? ViewFilterNote { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string? Note { get; set; }

    public List<long> OnlyOracle { get; set; } = [];

    public List<long> OnlyPanel { get; set; } = [];

    public List<string> Facts { get; set; } = [];

    public bool PanelMatchesOracle => Panel == Oracle && OnlyOracle.Count == 0 && OnlyPanel.Count == 0;

    public string Line()
    {
        var value = Value.Length == 0 ? string.Empty : $" '{Value}'";
        return $"{Document} / {Category} / {Parameter} {Operator}{value}: oracle {Oracle.ToString(CultureInfo.InvariantCulture)}, panel {Panel.ToString(CultureInfo.InvariantCulture)}{(Note is null ? string.Empty : " (" + Note + ")")}";
    }
}

internal sealed class ScheduleRow
{
    public string Category { get; set; } = string.Empty;

    public int Oracle { get; set; }

    public int Panel { get; set; }

    public int? Schedule { get; set; }

    public string Reason { get; set; } = string.Empty;
}

internal static class SweepLimits
{
    public static int Read(string variable, int fallback)
    {
        return int.TryParse(Environment.GetEnvironmentVariable(variable), NumberStyles.Integer, CultureInfo.InvariantCulture, out var wanted) && wanted > 0
            ? wanted
            : fallback;
    }
}
