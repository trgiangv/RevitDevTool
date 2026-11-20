using DevTools.ModelsContext.Model;
using RuleNode = DevTools.ModelsContext.Model.FilterRule;

namespace RevitDevTool.ModelsContext.LiveTests;

internal sealed class TreeBuilder
{
    private const double ExactRange = 1e-9;

    private readonly Adapter _adapter;
    private readonly IReadOnlyList<SampleDocument> _documents;
    private readonly IReadOnlyList<long> _groups;
    private readonly List<SourceId> _known;
    private readonly int _maxDepth;
    private readonly IReadOnlyList<ParameterInfo> _parameters;
    private readonly Random _random;
    private readonly Dictionary<ParameterKey, Tally> _tallies = [];
    private int _rulesLeft;

    public TreeBuilder(Random random, Adapter adapter, IReadOnlyList<SampleDocument> documents,
        IReadOnlyList<ParameterInfo> parameters, IReadOnlyList<long> groups, int maxDepth, IEnumerable<SourceId> known)
    {
        _random = random;
        _adapter = adapter;
        _documents = documents;
        _parameters = parameters;
        _groups = groups;
        _maxDepth = maxDepth;
        _known = known.OrderBy(source => source.Value, StringComparer.Ordinal).ToList();
    }

    public FilterDefinition Build(int ruleBudget)
    {
        _rulesLeft = ruleBudget;
        return new FilterDefinition(_groups, Group(1, false, ruleBudget > 20 ? 10 : 4));
    }

    private FilterSet Group(int depth, bool scoped, int width)
    {
        var match = _random.Next(2) == 0 ? SetMatch.All : SetMatch.Any;
        var children = new List<FilterNode>();
        var count = 1 + _random.Next(width);
        for (var index = 0; index < count && _rulesLeft > 0; index++)
        {
            if (depth < _maxDepth && _random.NextDouble() < 0.3)
            {
                children.Add(Group(depth + 1, true, width));
            }
            else if (_parameters.Count > 0)
            {
                children.Add(Rule(_parameters[_random.Next(_parameters.Count)]));
                _rulesLeft--;
            }
        }

        return new FilterSet(match, children, scoped ? Scope() : null);
    }

    private FilterScope? Scope()
    {
        var documents = new List<SourceId>();
        if (_random.NextDouble() < 0.4)
        {
            documents.Add(_known[_random.Next(_known.Count)]);
            documents.AddRange(_known.Where(source => !documents.Contains(source) && _random.NextDouble() < 0.4));
        }

        var categories = new List<long>();
        if (_random.NextDouble() < 0.4)
        {
            categories.Add(_groups[_random.Next(_groups.Count)]);
            categories.AddRange(_groups.Where(group => !categories.Contains(group) && _random.NextDouble() < 0.3));
        }

        var scope = FilterScope.Of(documents, categories);
        return scope.IsSame ? null : scope;
    }

    private RuleNode Rule(ParameterInfo info)
    {
        var kind = info.Kind;
        var operators = FilterOperators.For(kind).Where(op => kind != ValueKind.Reference || !FilterOperators.NeedsValue(op)).ToList();
        var op = operators[_random.Next(operators.Count)];
        var key = info.Key;
        if (!FilterOperators.NeedsValue(op))
        {
            return new RuleNode(key, op);
        }

        var tally = TallyOf(key);
        return kind switch
        {
            ValueKind.Text => TextRule(key, op, tally),
            ValueKind.Number => NumberRule(key, op, tally),
            ValueKind.YesNo => new RuleNode(key, op, _random.Next(2) == 0 ? ValueKinds.Yes : ValueKinds.No),
            ValueKind.Integer => NamedRule(key, op, tally.Texts.Where(text => int.TryParse(text, out _)).ToList()),
            _ => NamedRule(key, op, tally.Texts)
        };
    }

    private RuleNode TextRule(ParameterKey key, FilterOperator op, Tally tally)
    {
        if (tally.Texts.Count == 0)
        {
            return new RuleNode(key, FilterOperator.Defined);
        }

        var text = tally.Texts[_random.Next(tally.Texts.Count)];
        var caseSensitive = _random.Next(3) == 0;
        var target = op switch
        {
            FilterOperator.Contains or FilterOperator.NotContains => Middle(text),
            FilterOperator.BeginsWith or FilterOperator.NotBeginsWith => text.Substring(0, 1 + _random.Next(text.Length)),
            FilterOperator.EndsWith or FilterOperator.NotEndsWith => text.Substring(text.Length - 1 - _random.Next(text.Length)),
            _ => text
        };
        if (caseSensitive && _random.Next(2) == 0)
        {
            target = target == target.ToUpperInvariant() ? target.ToLowerInvariant() : target.ToUpperInvariant();
        }

        return new RuleNode(key, op, target, caseSensitive);
    }

    private string Middle(string text)
    {
        var start = _random.Next(text.Length);
        return text.Substring(start, 1 + _random.Next(text.Length - start));
    }

    private RuleNode NumberRule(ParameterKey key, FilterOperator op, Tally tally)
    {
        if (op is FilterOperator.Equal or FilterOperator.NotEqual)
        {
            if (tally.Ranges.Count == 0)
            {
                return new RuleNode(key, FilterOperator.Defined);
            }

            var text = tally.Ranges.Keys.OrderBy(known => known, StringComparer.Ordinal).ElementAt(_random.Next(tally.Ranges.Count));
            return new RuleNode(key, op, text, false, tally.Ranges[text].Range);
        }

        var exact = _documents.All(document => document.Key.IsCurrentModel)
            ? tally.Ranges.Where(pair => pair.Value.Exact).Select(pair => pair.Key).OrderBy(known => known, StringComparer.Ordinal).ToList()
            : [];
        return exact.Count == 0
            ? new RuleNode(key, FilterOperator.NotEmpty)
            : new RuleNode(key, op, exact[_random.Next(exact.Count)]);
    }

    private RuleNode NamedRule(ParameterKey key, FilterOperator op, IReadOnlyList<string> texts)
    {
        return texts.Count == 0
            ? new RuleNode(key, FilterOperator.Defined)
            : new RuleNode(key, op, texts[_random.Next(texts.Count)]);
    }

    private Tally TallyOf(ParameterKey key)
    {
        if (_tallies.TryGetValue(key, out var known))
        {
            return known;
        }

        var texts = new SortedSet<string>(StringComparer.Ordinal);
        var ranges = new Dictionary<string, (NumberRange Range, bool Exact)>(StringComparer.Ordinal);
        foreach (var source in _documents.Select(document => document.Source).Distinct())
        {
            var counts = Adapter.Run(_adapter.Evaluator.CountValuesAsync(source, key, _groups, CancellationToken.None));
            foreach (var text in counts.Values.Keys.Where(text => !string.IsNullOrWhiteSpace(text)))
            {
                texts.Add(text);
            }

            foreach (var pair in counts.Numbers ?? new Dictionary<string, NumberRange>())
            {
                var exact = pair.Value.Max - pair.Value.Min < ExactRange;
                ranges[pair.Key] = ranges.TryGetValue(pair.Key, out var before)
                    ? (before.Range.Union(pair.Value), before.Exact && exact)
                    : (pair.Value, exact);
            }
        }

        var tally = new Tally([.. texts], ranges);
        _tallies.Add(key, tally);
        return tally;
    }

    private sealed record Tally(IReadOnlyList<string> Texts, Dictionary<string, (NumberRange Range, bool Exact)> Ranges);
}
