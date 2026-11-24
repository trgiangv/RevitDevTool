using DevTools.ModelsContext.Filters;
using Nice3point.Revit.Extensions;
using RevitFilterRule = Autodesk.Revit.DB.FilterRule;

namespace RevitDevTool.Tools.ModelsContext.Filters;

internal static class RevitRuleFactory
{
    public static Compiled InCategories(FilterScope? scope)
    {
        return scope is { Categories.Count: > 0 } categories
            ? new Compiled.Filter(new ElementMulticategoryFilter(categories.Categories.Select(id => id.ToElementId()).ToList()))
            : Compiled.All;
    }

    public static Compiled Exists(ElementId id)
    {
        return Compiled.Or([HasValue(id), HasNoValue(id)]);
    }

    public static Compiled HasValue(ElementId id)
    {
        return Pass(ParameterFilterRuleFactory.CreateHasValueParameterRule(id));
    }

    public static Compiled HasNoValue(ElementId id)
    {
        return Pass(ParameterFilterRuleFactory.CreateHasNoValueParameterRule(id));
    }

    public static Compiled HasValue(ElementId id, StorageType storage)
    {
        return storage switch
        {
            StorageType.ElementId => Compiled.And([HasValue(id), Pass(ParameterFilterRuleFactory.CreateNotEqualsRule(id, ElementId.InvalidElementId))]),
            StorageType.String => Compiled.And([HasValue(id), Pass(ParameterFilterRuleFactory.CreateNotEqualsRule(id, string.Empty))]),
            _ => HasValue(id)
        };
    }

    public static Compiled HasNoValue(ElementId id, StorageType storage)
    {
        return storage switch
        {
            StorageType.ElementId => Compiled.Or([HasNoValue(id), Compiled.And([HasValue(id),
                Pass(ParameterFilterRuleFactory.CreateEqualsRule(id, ElementId.InvalidElementId))])]),
            StorageType.String => Compiled.Or([HasNoValue(id), Compiled.And([HasValue(id),
                Pass(ParameterFilterRuleFactory.CreateEqualsRule(id, string.Empty))])]),
            _ => HasNoValue(id)
        };
    }

    public static Compiled Holds(ElementId id, StorageType storage)
    {
        return storage switch
        {
            StorageType.Integer => Pass(ParameterFilterRuleFactory.CreateGreaterOrEqualRule(id, int.MinValue)),
            StorageType.Double => Pass(ParameterFilterRuleFactory.CreateGreaterOrEqualRule(id, double.MinValue, 0)),
            _ => Compiled.None
        };
    }

    public static Compiled Text(ElementId parameter, FilterOperator op, string value)
    {
        RevitFilterRule? created = op switch
        {
            FilterOperator.Equal => ParameterFilterRuleFactory.CreateEqualsRule(parameter, value),
            FilterOperator.NotEqual => ParameterFilterRuleFactory.CreateNotEqualsRule(parameter, value),
            FilterOperator.Greater => ParameterFilterRuleFactory.CreateGreaterRule(parameter, value),
            FilterOperator.GreaterOrEqual => ParameterFilterRuleFactory.CreateGreaterOrEqualRule(parameter, value),
            FilterOperator.Less => ParameterFilterRuleFactory.CreateLessRule(parameter, value),
            FilterOperator.LessOrEqual => ParameterFilterRuleFactory.CreateLessOrEqualRule(parameter, value),
            FilterOperator.Contains => ParameterFilterRuleFactory.CreateContainsRule(parameter, value),
            FilterOperator.NotContains => ParameterFilterRuleFactory.CreateNotContainsRule(parameter, value),
            FilterOperator.BeginsWith => ParameterFilterRuleFactory.CreateBeginsWithRule(parameter, value),
            FilterOperator.NotBeginsWith => ParameterFilterRuleFactory.CreateNotBeginsWithRule(parameter, value),
            FilterOperator.EndsWith => ParameterFilterRuleFactory.CreateEndsWithRule(parameter, value),
            FilterOperator.NotEndsWith => ParameterFilterRuleFactory.CreateNotEndsWithRule(parameter, value),
            _ => null
        };
        return created is null ? Compiled.None : Pass(created);
    }

    public static Compiled Integer(ElementId parameter, FilterOperator op, int value)
    {
        RevitFilterRule? created = op switch
        {
            FilterOperator.Equal => ParameterFilterRuleFactory.CreateEqualsRule(parameter, value),
            FilterOperator.NotEqual => ParameterFilterRuleFactory.CreateNotEqualsRule(parameter, value),
            FilterOperator.Greater => ParameterFilterRuleFactory.CreateGreaterRule(parameter, value),
            FilterOperator.GreaterOrEqual => ParameterFilterRuleFactory.CreateGreaterOrEqualRule(parameter, value),
            FilterOperator.Less => ParameterFilterRuleFactory.CreateLessRule(parameter, value),
            FilterOperator.LessOrEqual => ParameterFilterRuleFactory.CreateLessOrEqualRule(parameter, value),
            _ => null
        };
        return created is null ? Compiled.None : Pass(created);
    }

    public static Compiled Number(ElementId parameter, FilterOperator op, double value, double tolerance)
    {
        RevitFilterRule? created = op switch
        {
            FilterOperator.Equal => ParameterFilterRuleFactory.CreateEqualsRule(parameter, value, tolerance),
            FilterOperator.NotEqual => ParameterFilterRuleFactory.CreateNotEqualsRule(parameter, value, tolerance),
            FilterOperator.Greater => ParameterFilterRuleFactory.CreateGreaterRule(parameter, value, tolerance),
            FilterOperator.GreaterOrEqual => ParameterFilterRuleFactory.CreateGreaterOrEqualRule(parameter, value, tolerance),
            FilterOperator.Less => ParameterFilterRuleFactory.CreateLessRule(parameter, value, tolerance),
            FilterOperator.LessOrEqual => ParameterFilterRuleFactory.CreateLessOrEqualRule(parameter, value, tolerance),
            _ => null
        };
        return created is null ? Compiled.None : Pass(created);
    }

    public static Compiled Reference(ElementId parameter, FilterOperator op, IReadOnlyList<long> targets)
    {
        return op switch
        {
            FilterOperator.Equal => Compiled.Or(targets.Select(item =>
                Pass(ParameterFilterRuleFactory.CreateEqualsRule(parameter, item.ToElementId())))),
            FilterOperator.NotEqual => Compiled.And(targets.Select(item =>
                Pass(ParameterFilterRuleFactory.CreateNotEqualsRule(parameter, item.ToElementId())))),
            _ => Compiled.None
        };
    }

    private static Compiled Pass(RevitFilterRule rule)
    {
        return new Compiled.Filter(new ElementParameterFilter(rule));
    }
}
