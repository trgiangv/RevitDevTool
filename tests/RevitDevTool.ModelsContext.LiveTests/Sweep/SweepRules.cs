using DevTools.ModelsContext.Model;
using System.Globalization;
using RuleNode = DevTools.ModelsContext.Model.FilterRule;

namespace RevitDevTool.ModelsContext.LiveTests;

internal sealed record SweepRules(IReadOnlyList<RuleNode> Rules, bool SkippedLinkMedian, string? Note);

internal static class SweepRuleSet
{
    public const int PerParameter = 5;

    private const double ExactRange = 1e-9;

    public static SweepRules For(Adapter adapter, SampleDocument document, long category, ParameterInfo parameter)
    {
        var key = parameter.Key;
        var rules = new List<RuleNode> { new(key, FilterOperator.NotEmpty), new(key, FilterOperator.Empty) };
        if (parameter.Kind == ValueKind.Reference)
        {
            return new SweepRules(rules, false, null);
        }

        ParameterValueCounts counts;
        try
        {
            counts = Adapter.Run(adapter.Evaluator.CountValuesAsync(document.Source, key, [category], CancellationToken.None));
        }
        catch (Exception exception) when (exception is not AssertionException)
        {
            return new SweepRules(rules, false, "value list failed: " + exception.Message);
        }

        var common = counts.Values.Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => pair.Key)
            .FirstOrDefault();
        var skippedMedian = false;
        switch (parameter.Kind)
        {
            case ValueKind.Text when common is not null:
                rules.Add(new RuleNode(key, FilterOperator.Equal, common));
                rules.Add(new RuleNode(key, FilterOperator.Contains, Middle(common)));
                break;
            case ValueKind.Number:
                if (common is not null && counts.Numbers is { } numbers && numbers.TryGetValue(common, out var range))
                {
                    rules.Add(new RuleNode(key, FilterOperator.Equal, common, false, range));
                }

                if (!document.Key.IsCurrentModel)
                {
                    skippedMedian = true;
                }
                else if (Median(counts.Numbers) is { } median)
                {
                    rules.Add(new RuleNode(key, FilterOperator.GreaterOrEqual, median));
                }

                break;
            case ValueKind.Integer:
                if (common is not null && int.TryParse(common, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                {
                    rules.Add(new RuleNode(key, FilterOperator.Equal, common));
                }

                var integers = counts.Values.Keys
                    .Select(text => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? (int?)number : null)
                    .OfType<int>()
                    .OrderBy(number => number)
                    .ToList();
                if (integers.Count > 0)
                {
                    rules.Add(new RuleNode(key, FilterOperator.GreaterOrEqual, integers[integers.Count / 2].ToString(CultureInfo.InvariantCulture)));
                }

                break;
            case ValueKind.YesNo:
                var no = common is not null && ValueKinds.TryParseYesNo(common, out var flag) && flag == 0;
                rules.Add(new RuleNode(key, FilterOperator.Equal, no ? ValueKinds.No : ValueKinds.Yes));
                break;
            case ValueKind.Enumerated when common is not null:
                rules.Add(new RuleNode(key, FilterOperator.Equal, common));
                break;
        }

        return new SweepRules(rules, skippedMedian, null);
    }

    public static string Middle(string text)
    {
        var start = text.Length / 3;
        return text.Substring(start, Math.Max(1, text.Length / 3));
    }

    private static string? Median(IReadOnlyDictionary<string, NumberRange>? numbers)
    {
        var exact = (numbers ?? new Dictionary<string, NumberRange>())
            .Where(pair => pair.Value.Max - pair.Value.Min < ExactRange)
            .OrderBy(pair => pair.Value.Middle)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => pair.Key)
            .ToList();
        return exact.Count == 0 ? null : exact[exact.Count / 2];
    }
}
