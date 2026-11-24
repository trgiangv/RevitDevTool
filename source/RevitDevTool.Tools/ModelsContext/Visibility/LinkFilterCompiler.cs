using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Parameters;
using DevTools.ModelsContext.Visibility;
using Nice3point.Revit.Extensions;
using RevitDevTool.Tools.ModelsContext.Filters;
using RevitDevTool.Tools.ModelsContext.Parameters;
using RevitFilterRule = Autodesk.Revit.DB.FilterRule;
using RuleNode = DevTools.ModelsContext.Filters.FilterRule;

namespace RevitDevTool.Tools.ModelsContext.Visibility;

internal sealed class LinkFilterCompiler(Document model, Document link, ICollection<ElementId> categories)
{
    private static readonly Dictionary<string, BuiltInParameter[]> Alternates = new(StringComparer.Ordinal)
    {
        [nameof(BuiltInParameter.ALL_MODEL_FAMILY_NAME)] =
            [BuiltInParameter.ALL_MODEL_FAMILY_NAME, BuiltInParameter.SYMBOL_FAMILY_NAME_PARAM],
        [nameof(BuiltInParameter.ALL_MODEL_TYPE_NAME)] =
            [BuiltInParameter.ALL_MODEL_TYPE_NAME, BuiltInParameter.SYMBOL_NAME_PARAM]
    };

    private readonly HashSet<long> _filterable = new(ParameterFilterUtilities
        .GetFilterableParametersInCommon(model, categories)
        .Select(id => id.ToLong()));

    private readonly RuleValueParser _parser = RuleValueParser.For(model.GetUnits());
    private readonly Dictionary<ParameterKey, (ElementId Id, StorageType Storage, ForgeTypeId? Spec)> _resolved = [];

    public ElementFilter? Compile(FilterSet root)
    {
        return Set(root);
    }

    private ElementFilter? Set(FilterSet set)
    {
        var parts = new List<ElementFilter>();
        foreach (var child in set.Children)
        {
            var part = child switch
            {
                FilterSet nested => Set(nested),
                RuleNode rule => Leaf(rule),
                _ => throw new InvalidOperationException(VisibilityText.LinkRuleNotNative)
            };
            if (part is not null)
            {
                parts.Add(part);
            }
            else if (set.Match == SetMatch.Any)
            {
                return null;
            }
        }

        return parts.Count switch
        {
            0 => null,
            1 => parts[0],
            _ => set.Match == SetMatch.All ? new LogicalAndFilter(parts) : new LogicalOrFilter(parts)
        };
    }

    private ElementParameterFilter Leaf(RuleNode rule)
    {
        var (id, storage, spec) = Resolve(rule.Parameter);
        return new ElementParameterFilter(rule.Operator switch
        {
            FilterOperator.NotEmpty => ParameterFilterRuleFactory.CreateHasValueParameterRule(id),
            FilterOperator.Empty => ParameterFilterRuleFactory.CreateHasNoValueParameterRule(id),
            _ => storage switch
            {
                StorageType.String => Text(id, rule.Operator, rule.Value),
                StorageType.Integer => Integer(id, rule, spec),
                StorageType.Double => Number(id, rule, spec),
                _ => null
            }
        } ?? throw new InvalidOperationException(VisibilityText.LinkRuleNotNative));
    }

    private (ElementId Id, StorageType Storage, ForgeTypeId? Spec) Resolve(ParameterKey key)
    {
        if (!_resolved.TryGetValue(key, out var resolved))
        {
            resolved = ResolveNew(key);
            _resolved.Add(key, resolved);
        }

        return resolved;
    }

    private (ElementId Id, StorageType Storage, ForgeTypeId? Spec) ResolveNew(ParameterKey key)
    {
        var located = ParameterLocator.Locate(link, key, categories)
            .FirstOrDefault(parameter => parameter.Storage != StorageType.None);
        if (key.BuiltInName is { } name)
        {
            var (builtIn, storage) = ResolveBuiltIn(name);
            return (builtIn, storage, located?.Spec);
        }

        var hostId = ParameterLocator.IdsOf(model, key, categories, null).FirstOrDefault();
        if (located is null || hostId == 0 || !_filterable.Contains(hostId))
        {
            throw new InvalidOperationException(VisibilityText.LinkRuleNotNative);
        }

        return (hostId.ToElementId(), located.Storage, located.Spec);
    }

    private (ElementId Id, StorageType Storage) ResolveBuiltIn(string name)
    {
        var candidates = Alternates.TryGetValue(name, out var alternates)
            ? alternates
            : Enum.TryParse<BuiltInParameter>(name, out var parsed) ? [parsed] : [];
        foreach (var candidate in candidates.Where(candidate => _filterable.Contains((long)candidate)))
        {
            return (new ElementId(candidate), model.get_TypeOfStorage(candidate));
        }

        throw new InvalidOperationException(alternates is null ? VisibilityText.LinkRuleNotNative : VisibilityText.NoNameRule);
    }

    private static RevitFilterRule? Text(ElementId id, FilterOperator op, string value)
    {
        return op switch
        {
            FilterOperator.Equal => ParameterFilterRuleFactory.CreateEqualsRule(id, value),
            FilterOperator.NotEqual => ParameterFilterRuleFactory.CreateNotEqualsRule(id, value),
            FilterOperator.Greater => ParameterFilterRuleFactory.CreateGreaterRule(id, value),
            FilterOperator.GreaterOrEqual => ParameterFilterRuleFactory.CreateGreaterOrEqualRule(id, value),
            FilterOperator.Less => ParameterFilterRuleFactory.CreateLessRule(id, value),
            FilterOperator.LessOrEqual => ParameterFilterRuleFactory.CreateLessOrEqualRule(id, value),
            FilterOperator.Contains => ParameterFilterRuleFactory.CreateContainsRule(id, value),
            FilterOperator.NotContains => ParameterFilterRuleFactory.CreateNotContainsRule(id, value),
            FilterOperator.BeginsWith => ParameterFilterRuleFactory.CreateBeginsWithRule(id, value),
            FilterOperator.NotBeginsWith => ParameterFilterRuleFactory.CreateNotBeginsWithRule(id, value),
            FilterOperator.EndsWith => ParameterFilterRuleFactory.CreateEndsWithRule(id, value),
            FilterOperator.NotEndsWith => ParameterFilterRuleFactory.CreateNotEndsWithRule(id, value),
            _ => null
        };
    }

    private static RevitFilterRule? Integer(ElementId id, RuleNode rule, ForgeTypeId? spec)
    {
        int value;
        if (SpecInfo.IsYesNo(spec) && ValueKinds.TryParseYesNo(rule.Value, out var flag))
        {
            value = flag;
        }
        else if (!RuleValueParser.TryInteger(rule.Value, out value))
        {
            return null;
        }

        return rule.Operator switch
        {
            FilterOperator.Equal => ParameterFilterRuleFactory.CreateEqualsRule(id, value),
            FilterOperator.NotEqual => ParameterFilterRuleFactory.CreateNotEqualsRule(id, value),
            FilterOperator.Greater => ParameterFilterRuleFactory.CreateGreaterRule(id, value),
            FilterOperator.GreaterOrEqual => ParameterFilterRuleFactory.CreateGreaterOrEqualRule(id, value),
            FilterOperator.Less => ParameterFilterRuleFactory.CreateLessRule(id, value),
            FilterOperator.LessOrEqual => ParameterFilterRuleFactory.CreateLessOrEqualRule(id, value),
            _ => null
        };
    }

    private RevitFilterRule? Number(ElementId id, RuleNode rule, ForgeTypeId? spec)
    {
        RuleNumber number;
        if (rule.Picked is { } picked)
        {
            number = RuleValueParser.Picked(picked);
        }
        else if (!_parser.TryNumber(new ResolvedParameter(id.ToLong(), rule.Parameter, StorageType.Double, spec?.TypeId,
                     TypePresence.None, []), rule.Value, out number))
        {
            return null;
        }

        return rule.Operator switch
        {
            FilterOperator.Equal => ParameterFilterRuleFactory.CreateEqualsRule(id, number.Value, number.Tolerance),
            FilterOperator.NotEqual => ParameterFilterRuleFactory.CreateNotEqualsRule(id, number.Value, number.Tolerance),
            FilterOperator.Greater => ParameterFilterRuleFactory.CreateGreaterRule(id, number.Value, number.Tolerance),
            FilterOperator.GreaterOrEqual => ParameterFilterRuleFactory.CreateGreaterOrEqualRule(id, number.Value, number.Tolerance),
            FilterOperator.Less => ParameterFilterRuleFactory.CreateLessRule(id, number.Value, number.Tolerance),
            FilterOperator.LessOrEqual => ParameterFilterRuleFactory.CreateLessOrEqualRule(id, number.Value, number.Tolerance),
            _ => null
        };
    }
}
