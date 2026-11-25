using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Scope;
using Nice3point.Revit.Extensions;
using System.Globalization;
using RuleNode = DevTools.ModelsContext.Filters.FilterRule;

namespace RevitDevTool.ModelsContext.LiveTests;

internal static class Oracle
{
    public const double Tolerance = 1e-6;

    private const int DeclaredLimit = 5_000;

    private static readonly Dictionary<(string Title, long Element), HashSet<long>> DeclaredIds = [];

    private static readonly HashSet<BuiltInCategory> NotElementCategories =
    [
        BuiltInCategory.OST_RvtLinks,
        BuiltInCategory.OST_Materials
    ];

    private static readonly HashSet<BuiltInCategory> NoSubcategoryRows =
    [
        BuiltInCategory.OST_Site,
        BuiltInCategory.OST_Lines,
        BuiltInCategory.OST_Topography
    ];

    public static int Count(FilterDefinition definition, IReadOnlyList<SampleDocument> documents, ScopeKind scope)
    {
        return Matches(definition, documents, scope).Count;
    }

    public static List<ElementRef> Matches(FilterDefinition definition, IReadOnlyList<SampleDocument> documents, ScopeKind scope)
    {
        return Matches(definition, documents, scope, SampleModel.Host, SampleModel.KnownSources);
    }

    public static List<ElementRef> Matches(FilterDefinition definition, IReadOnlyList<SampleDocument> documents, ScopeKind scope,
        Document host, ICollection<SourceId> known)
    {
        var units = host.GetUnits();
        var matches = new List<ElementRef>();
        foreach (var document in documents)
        {
            var view = scope == ScopeKind.ActiveView ? ViewIds(document, host) : null;
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
            if (category.CategoryType != CategoryType.Model || NotElementCategories.Contains(category.BuiltInCategory))
            {
                continue;
            }

            ids.Add(category.Id.ToLong());
            if (NoSubcategoryRows.Contains(category.BuiltInCategory))
            {
                continue;
            }

            foreach (Category subcategory in category.SubCategories)
            {
                if (subcategory.CategoryType == CategoryType.Model && subcategory.BuiltInCategory != BuiltInCategory.INVALID
                    && subcategory.BuiltInCategory.ToString().IndexOf("CenterLine", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    ids.Add(subcategory.Id.ToLong());
                }
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
        return ViewIds(document, SampleModel.Host);
    }

    public static HashSet<long> ViewIds(SampleDocument document, Document host)
    {
        var view = SampleModel.Application.ActiveUIDocument.ActiveView;
        var collector = document.Key.IsCurrentModel
            ? new FilteredElementCollector(host, view.Id)
            : new FilteredElementCollector(host, view.Id, document.Key.LinkInstanceId.ToElementId());
        var ids = new HashSet<long>(collector.WhereElementIsNotElementType().ToElementIds().Select(id => id.ToLong()));
        ids.UnionWith(RailingPartsOf(document.Document, view, ids));
        return ids;
    }

    private static List<long> RailingPartsOf(Document document, View view, HashSet<long> drawn)
    {
        BuiltInCategory[] railings = [BuiltInCategory.OST_StairsRailing, BuiltInCategory.OST_RailingHandRail, BuiltInCategory.OST_RailingTopRail];
        BuiltInCategory[] parts = [BuiltInCategory.OST_StairsRailingBaluster, BuiltInCategory.OST_RailingSupport, BuiltInCategory.OST_RailingTermination];
        var found = new List<long>();
        foreach (var id in drawn)
        {
            if (document.GetElement(id.ToElementId()) is not { Category: { } category } railing
                || !railings.Contains(category.BuiltInCategory))
            {
                continue;
            }

            found.AddRange(railing.GetDependentElements(null)
                .Select(document.GetElement)
                .Where(part => part?.Category is { } partCategory && parts.Contains(partCategory.BuiltInCategory)
                               && !CategoryOff(view, partCategory.Id))
                .Select(part => part!.Id.ToLong()));
        }

        return found;
    }

    private static bool CategoryOff(View view, ElementId category)
    {
        try
        {
            return view.GetCategoryHidden(category);
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException)
        {
            return false;
        }
    }

    internal static bool Matches(FilterNode node, Element element, SourceId source, ICollection<SourceId> known, Units units)
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
            FilterOperator.NotEqual when !filled => true,
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
                return filled && Number(value, rule.Value, units) is { } target && Compare(rule.Operator, CompareShown(value, target, units));
            case StorageType.ElementId:
                return filled && value.AsElementId() != ElementId.InvalidElementId
                              && EqualityMatches(rule.Operator, Refers(value, rule.Value.Trim()));
            default:
                return false;
        }
    }

    public static (Parameter? Instance, Parameter? Type) Read(Element element, ParameterKey key)
    {
        var owner = element.Document.GetElement(element.GetTypeId()) as ElementType;
        var type = owner is null ? null : Find(owner, key);
        return (Find(element, key, owner), type);
    }

    private static bool NativelyHeld(Element element, Parameter parameter)
    {
        var parameterId = parameter.Id;
        var rules = new List<Autodesk.Revit.DB.FilterRule>
        {
            ParameterFilterRuleFactory.CreateHasValueParameterRule(parameterId),
            ParameterFilterRuleFactory.CreateHasNoValueParameterRule(parameterId)
        };
        if (parameter.StorageType == StorageType.Integer)
        {
            rules.Add(ParameterFilterRuleFactory.CreateGreaterOrEqualRule(parameterId, int.MinValue));
        }
        else if (parameter.StorageType == StorageType.Double)
        {
            rules.Add(ParameterFilterRuleFactory.CreateGreaterOrEqualRule(parameterId, double.MinValue, 0));
        }

        return rules.Any(rule => new ElementParameterFilter(rule).PassesFilter(element));
    }

    public static Parameter? Find(Element element, ParameterKey key)
    {
        return Find(element, key, null);
    }

    private static Parameter? Find(Element element, ParameterKey key, ElementType? owner)
    {
        if (key.BuiltInName is { } builtIn)
        {
            return Carried(element, element.get_Parameter((BuiltInParameter)Enum.Parse(typeof(BuiltInParameter), builtIn)), owner);
        }

        if (key.SharedId is { } shared)
        {
            return Carried(element, element.get_Parameter(shared), owner);
        }

        var named = element.GetParameters(key.Name)
            .Where(parameter => !parameter.IsShared && parameter.Id.ToLong() > 0 && DataType(parameter) == key.DataType)
            .ToList();
        return named.FirstOrDefault(HasValue) ?? named.FirstOrDefault();
    }

    private static Parameter? Carried(Element element, Parameter? parameter, ElementType? owner)
    {
        if (parameter is null)
        {
            return null;
        }

        var id = parameter.Id.ToLong();
        if ((owner is null || !Declared(owner).Contains(id)) && NativelyHeld(element, parameter))
        {
            return parameter;
        }

        return Declared(element).Contains(id) ? parameter : null;
    }

    private static HashSet<long> Declared(Element element)
    {
        var key = (element.Document.Title, element.Id.ToLong());
        if (!DeclaredIds.TryGetValue(key, out var ids))
        {
            if (DeclaredIds.Count >= DeclaredLimit)
            {
                DeclaredIds.Clear();
            }

            ids = [];
            foreach (Parameter parameter in element.Parameters)
            {
                ids.Add(parameter.Id.ToLong());
            }

            DeclaredIds.Add(key, ids);
        }

        return ids;
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

    public static double HalfStep(Units units, ForgeTypeId spec)
    {
        try
        {
            var options = units.GetFormatOptions(spec);
            var unit = options.GetUnitTypeId();
            return Math.Abs(UnitUtils.ConvertToInternalUnits(options.Accuracy, unit) - UnitUtils.ConvertToInternalUnits(0, unit)) / 2;
        }
        catch (Exception exception) when (exception is Autodesk.Revit.Exceptions.ApplicationException or ArgumentException)
        {
            return 0;
        }
    }

    private static int CompareShown(Parameter parameter, double target, Units units)
    {
        var value = parameter.AsDouble();
        var spec = parameter.Definition.GetDataType();
        if (string.IsNullOrEmpty(spec.TypeId) || !UnitUtils.IsMeasurableSpec(spec))
        {
            return CompareDouble(value, target, Tolerance);
        }

        var half = HalfStep(units, spec);
        if (!(half > Tolerance))
        {
            return CompareDouble(value, target, Tolerance);
        }

        return Shown(units, spec, target) is { } grid && Math.Abs(grid - target) <= Math.Min(Tolerance, half / 4)
                                                      && Shown(units, spec, value) is { } shown
            ? CompareDouble(shown, target, half / 2)
            : CompareDouble(value, target, half);
    }

    private static double? Shown(Units units, ForgeTypeId spec, double value)
    {
        var text = UnitFormatUtils.Format(units, spec, value, false);
        return UnitFormatUtils.TryParse(units, spec, text, out double parsed) ? parsed : null;
    }

    private static int CompareDouble(double value, double target, double tolerance)
    {
        return Math.Abs(value - target) <= tolerance ? 0 : value.CompareTo(target);
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
