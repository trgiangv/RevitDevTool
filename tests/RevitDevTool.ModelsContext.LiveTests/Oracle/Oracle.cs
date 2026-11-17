using DevTools.ModelsContext.Model;
using Nice3point.Revit.Extensions;
using System.Globalization;
using RuleNode = DevTools.ModelsContext.Model.FilterRule;

namespace RevitDevTool.ModelsContext.LiveTests;

internal static class Oracle
{
    public const double Tolerance = 1e-6;

    public static int Count(FilterDefinition definition, IReadOnlyList<SampleDocument> documents, ScopeKind scope)
    {
        return Matches(definition, documents, scope).Count;
    }

    public static List<ElementRef> Matches(FilterDefinition definition, IReadOnlyList<SampleDocument> documents, ScopeKind scope)
    {
        var units = SampleModel.Host.GetUnits();
        var known = SampleModel.KnownSources;
        var matches = new List<ElementRef>();
        foreach (var document in documents)
        {
            var view = scope == ScopeKind.ActiveView ? ViewIds(document) : null;
            foreach (var element in Candidates(document.Document, definition.Groups, view is not null))
            {
                if (view is not null && !view.Contains(element.Id.ToLong()))
                {
                    continue;
                }

                if (Matches(definition.Root, element, document.Source, known, units))
                {
                    matches.Add(new ElementRef(document.Key, element.Id.ToLong()));
                }
            }
        }

        return matches;
    }

    public static IEnumerable<Element> Candidates(Document document, IReadOnlyCollection<long> categories, bool anyOption = false)
    {
        var wanted = categories.Count > 0 ? new HashSet<long>(categories) : ModelCategories(document);
        return new FilteredElementCollector(document).WhereElementIsNotElementType()
            .Where(element => element.Category is { } category && wanted.Contains(category.Id.ToLong()) && (anyOption || InMainOrPrimary(element)));
    }

    public static HashSet<long> ModelCategories(Document document)
    {
        var ids = new HashSet<long>();
        foreach (Category category in document.Settings.Categories)
        {
            if (category.CategoryType == CategoryType.Model && category.BuiltInCategory != BuiltInCategory.OST_RvtLinks)
            {
                ids.Add(category.Id.ToLong());
            }
        }

        return ids;
    }

    public static bool InMainOrPrimary(Element element)
    {
        return element.DesignOption is not { } option || option.IsPrimary;
    }

    public static HashSet<long> ViewIds(SampleDocument document)
    {
        var host = SampleModel.Host;
        var view = SampleModel.Application.ActiveUIDocument.ActiveView;
        var collector = document.Key.IsCurrentModel
            ? new FilteredElementCollector(host, view.Id)
            : new FilteredElementCollector(host, view.Id, document.Key.LinkInstanceId.ToElementId());
        return [.. collector.WhereElementIsNotElementType().ToElementIds().Select(id => id.ToLong())];
    }

    private static bool Matches(FilterNode node, Element element, SourceId source, ICollection<SourceId> known, Units units)
    {
        return node switch
        {
            FilterSet set => SetMatches(set, element, source, known, units),
            RuleNode rule => RuleMatches(rule, element, units),
            _ => true
        };
    }

    private static bool SetMatches(FilterSet set, Element element, SourceId source, ICollection<SourceId> known, Units units)
    {
        if (set.Scope is { } scope)
        {
            if (scope.Documents.Any(document => !known.Contains(document))
                || (scope.Documents.Count > 0 && !scope.Documents.Contains(source))
                || (scope.Categories.Count > 0 && !scope.Categories.Contains(element.Category.Id.ToLong())))
            {
                return false;
            }
        }

        if (set.Children.Count == 0)
        {
            return true;
        }

        return set.Match == SetMatch.All
            ? set.Children.All(child => Matches(child, element, source, known, units))
            : set.Children.Any(child => Matches(child, element, source, known, units));
    }

    private static bool RuleMatches(RuleNode rule, Element element, Units units)
    {
        var (instance, type) = Read(element, rule.Parameter);
        var defined = instance is not null || type is not null;
        if (rule.Operator == FilterOperator.Undefined)
        {
            return !defined;
        }

        if (!defined)
        {
            return false;
        }

        var value = HasValue(instance) ? instance! : HasValue(type) ? type! : (instance ?? type)!;
        var filled = HasValue(value);
        return rule.Operator switch
        {
            FilterOperator.Defined => true,
            FilterOperator.NotEmpty => filled,
            FilterOperator.Empty => !filled,
            _ => ValueMatches(rule, value, filled, units)
        };
    }

    private static bool ValueMatches(RuleNode rule, Parameter value, bool filled, Units units)
    {
        switch (value.StorageType)
        {
            case StorageType.String:
                return TextMatches(rule.Operator, value.AsString() ?? string.Empty, rule.Value, rule.IsCaseSensitive);
            case StorageType.Integer when IsYesNo(value):
                return filled && YesNo(rule.Value) is { } flag && Compare(rule.Operator, value.AsInteger().CompareTo(flag));
            case StorageType.Integer when int.TryParse(rule.Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number):
                return filled && Compare(rule.Operator, value.AsInteger().CompareTo(number));
            case StorageType.Integer:
                return filled && EqualityMatches(rule.Operator, string.Equals(value.AsValueString(), rule.Value.Trim(), StringComparison.Ordinal));
            case StorageType.Double when rule.Picked is not null:
                return filled && EqualityMatches(rule.Operator, string.Equals(value.AsValueString(), rule.Value.Trim(), StringComparison.Ordinal));
            case StorageType.Double:
                return filled && Number(value, rule.Value, units) is { } target && Compare(rule.Operator, CompareDouble(value.AsDouble(), target));
            case StorageType.ElementId:
                return filled && value.AsElementId() != ElementId.InvalidElementId
                              && EqualityMatches(rule.Operator, Refers(value, rule.Value.Trim()));
            default:
                return false;
        }
    }

    public static (Parameter? Instance, Parameter? Type) Read(Element element, ParameterKey key)
    {
        var instance = Find(element, key);
        var type = element.Document.GetElement(element.GetTypeId()) is ElementType elementType ? Find(elementType, key) : null;
        return (instance, type);
    }

    public static Parameter? Find(Element element, ParameterKey key)
    {
        if (key.BuiltInName is { } builtIn)
        {
            return element.get_Parameter((BuiltInParameter)Enum.Parse(typeof(BuiltInParameter), builtIn));
        }

        if (key.SharedId is { } shared)
        {
            return element.get_Parameter(shared);
        }

        return element.GetParameters(key.Name)
            .FirstOrDefault(parameter => !parameter.IsShared && parameter.Id.ToLong() > 0 && DataType(parameter) == key.DataType);
    }

    public static string? DataType(Parameter parameter)
    {
        var spec = parameter.Definition.GetDataType();
        return string.IsNullOrEmpty(spec.TypeId) ? parameter.StorageType.ToString() : WithoutVersion(spec.TypeId);
    }

    public static string WithoutVersion(string typeId)
    {
        var dash = typeId.LastIndexOf('-');
        return dash > typeId.IndexOf(':') ? typeId.Substring(0, dash) : typeId;
    }

    private static bool HasValue(Parameter? parameter)
    {
        if (parameter is null || !parameter.HasValue)
        {
            return false;
        }

        return parameter.StorageType switch
        {
            StorageType.String => !string.IsNullOrEmpty(parameter.AsString()),
            StorageType.ElementId => parameter.AsElementId() != ElementId.InvalidElementId,
            _ => true
        };
    }

    private static bool IsYesNo(Parameter parameter)
    {
        return DataType(parameter) == WithoutVersion(SpecTypeId.Boolean.YesNo.TypeId);
    }

    private static int? YesNo(string text)
    {
        return text.Trim().ToLowerInvariant() switch
        {
            "yes" or "true" or "1" => 1,
            "no" or "false" or "0" => 0,
            _ => null
        };
    }

    private static double? Number(Parameter parameter, string text, Units units)
    {
        var spec = parameter.Definition.GetDataType();
        if (!string.IsNullOrEmpty(spec.TypeId) && UnitUtils.IsMeasurableSpec(spec))
        {
            return UnitFormatUtils.TryParse(units, spec, text, out double parsed) ? parsed : null;
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var plain) ? plain : null;
    }

    private static int CompareDouble(double value, double target)
    {
        return Math.Abs(value - target) <= Tolerance ? 0 : value.CompareTo(target);
    }

    private static bool Refers(Parameter parameter, string text)
    {
        var id = parameter.AsElementId();
        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            return id.ToLong() == number;
        }

        return string.Equals(parameter.Element.Document.GetElement(id)?.Name, text, StringComparison.Ordinal);
    }

    private static bool Compare(FilterOperator op, int sign)
    {
        return op switch
        {
            FilterOperator.Equal => sign == 0,
            FilterOperator.NotEqual => sign != 0,
            FilterOperator.Greater => sign > 0,
            FilterOperator.GreaterOrEqual => sign >= 0,
            FilterOperator.Less => sign < 0,
            FilterOperator.LessOrEqual => sign <= 0,
            _ => false
        };
    }

    private static bool EqualityMatches(FilterOperator op, bool equal)
    {
        return op switch
        {
            FilterOperator.Equal => equal,
            FilterOperator.NotEqual => !equal,
            _ => false
        };
    }

    private static bool TextMatches(FilterOperator op, string text, string target, bool caseSensitive)
    {
        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return op switch
        {
            FilterOperator.Equal => string.Equals(text, target, comparison),
            FilterOperator.NotEqual => !string.Equals(text, target, comparison),
            FilterOperator.Greater => string.Compare(text, target, comparison) > 0,
            FilterOperator.GreaterOrEqual => string.Compare(text, target, comparison) >= 0,
            FilterOperator.Less => string.Compare(text, target, comparison) < 0,
            FilterOperator.LessOrEqual => string.Compare(text, target, comparison) <= 0,
            FilterOperator.Contains => text.IndexOf(target, comparison) >= 0,
            FilterOperator.NotContains => text.IndexOf(target, comparison) < 0,
            FilterOperator.BeginsWith => text.StartsWith(target, comparison),
            FilterOperator.NotBeginsWith => !text.StartsWith(target, comparison),
            FilterOperator.EndsWith => text.EndsWith(target, comparison),
            FilterOperator.NotEndsWith => !text.EndsWith(target, comparison),
            _ => false
        };
    }
}
