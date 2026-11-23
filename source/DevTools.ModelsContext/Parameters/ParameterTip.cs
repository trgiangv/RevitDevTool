using System.Globalization;

namespace DevTools.ModelsContext.Parameters;

public readonly record struct TipRow(string Label, string Value);

public sealed record TipList(string Title, IReadOnlyList<string> Items, string? More)
{
    public bool IsEmpty => Items.Count == 0;
}

public sealed record ParameterTip(string Title, IReadOnlyList<TipRow> Rows, TipList Models, TipList Places)
{
    public const int ListLimit = 5;
    public const string BuiltInLabel = "BuiltInParameter";
    public const string GuidLabel = "GUID";
    public const string DisciplineLabel = "Discipline";
    public const string TypeLabel = "Type of parameter";
    public const string GroupLabel = "Group";
    public const string ModelsTitle = "Models";
    public const string CategoriesTitle = "Categories";
    public const string FamiliesTitle = "Families";

    public static ParameterTip Of(ParameterInfo parameter)
    {
        var rows = new List<TipRow>(5);
        if (parameter.Key.BuiltInName is { Length: > 0 } builtIn)
        {
            rows.Add(new TipRow(BuiltInLabel, builtIn));
        }

        if (parameter.Key.SharedId is { } id)
        {
            rows.Add(new TipRow(GuidLabel, ShortGuid(id)));
        }

        Add(rows, DisciplineLabel, parameter.IsMeasured ? parameter.Discipline : null);
        Add(rows, TypeLabel, parameter.SpecLabel);
        Add(rows, GroupLabel, parameter.Group);
        var places = parameter.Source == ParameterOrigin.Family && parameter.Families.Count > 0
            ? List(FamiliesTitle, parameter.Families.OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase).ToList())
            : List(CategoriesTitle, parameter.CategoryNames);
        return new ParameterTip(parameter.DisplayName, rows, List(ModelsTitle, parameter.Models), places);
    }

    public static string ShortGuid(Guid id)
    {
        var text = id.ToString("D", CultureInfo.InvariantCulture);
        return text.Substring(0, 4) + "…" + text.Substring(text.Length - 4);
    }

    private static void Add(List<TipRow> rows, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            rows.Add(new TipRow(label, value!));
        }
    }

    private static TipList List(string title, IReadOnlyList<string> items)
    {
        if (items.Count <= ListLimit)
        {
            return new TipList(title, items, null);
        }

        var more = "+ " + (items.Count - ListLimit).ToString(CultureInfo.CurrentCulture) + " more";
        return new TipList(title, items.Take(ListLimit).ToList(), more);
    }
}
