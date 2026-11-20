using DevTools.ModelsContext.Model;
using Nice3point.Revit.Extensions;
using RuleNode = DevTools.ModelsContext.Model.FilterRule;

namespace RevitDevTool.ModelsContext.LiveTests;

internal sealed record NativeCheck(SweepRow Row, ElementFilter? Filter, long Category);

internal static class SweepNative
{
    public const string Same = "none";
    public const string Unexplained = "unexplained";
    public const string NoneReference = "none reference";

    public static ElementFilter? Run(SampleDocument document, long category, RuleNode rule, ParameterInfo parameter,
        HashSet<long> panel, SweepRow row)
    {
        ElementFilter? filter;
        HashSet<long> native;
        try
        {
            filter = NativeFilter.Compile(document.Document, FilterSet.All(rule), [category]);
            native = Collect(document.Document, category, filter);
        }
        catch (NotExpressibleException exception)
        {
            row.Reason = exception.Reason;
            row.Note = Join(row.Note, exception.Message);
            return null;
        }
        catch (Exception exception)
        {
            row.Reason = NativeFilter.Inexpressible;
            row.Note = Join(row.Note, "Revit refused the rule: " + exception.Message);
            return null;
        }

        row.Native = native.Count;
        row.Reason = native.SetEquals(panel) ? Same : Classify(document, rule, parameter, panel, native);
        return filter;
    }

    public static void ViewFilters(Document document, IReadOnlyList<NativeCheck> checks)
    {
        var usable = checks.Where(check => check.Filter is not null).ToList();
        if (usable.Count == 0)
        {
            return;
        }

        var filterable = new HashSet<long>(ParameterFilterUtilities.GetAllFilterableCategories().Select(id => id.ToLong()));
        using var transaction = new Transaction(document, "Model Context sweep view filters");
        transaction.Start();
        try
        {
            var index = 0;
            foreach (var check in usable)
            {
                index++;
                if (!filterable.Contains(check.Category))
                {
                    check.Row.ViewFilterNote = "the category cannot take a view filter";
                    continue;
                }

                try
                {
                    var categories = new HashSet<ElementId> { check.Category.ToElementId() };
                    if (!ParameterFilterElement.ElementFilterIsAcceptableForParameterFilterElement(document, categories, check.Filter!))
                    {
                        check.Row.ViewFilterNote = "Revit does not accept this rule as a view filter";
                        continue;
                    }

                    var element = ParameterFilterElement.Create(document, $"SW check {index} {Guid.NewGuid():N}", categories, check.Filter!);
                    var collector = new FilteredElementCollector(document).WhereElementIsNotElementType().OfCategoryId(check.Category.ToElementId());
                    if (element.GetElementFilter() is { } created)
                    {
                        collector = collector.WherePasses(created);
                    }

                    check.Row.ViewFilter = collector.GetElementCount();
                }
                catch (Exception exception)
                {
                    check.Row.ViewFilterNote = "Revit refused the view filter: " + exception.Message;
                }
            }
        }
        finally
        {
            if (transaction.HasStarted())
            {
                transaction.RollBack();
            }
        }
    }

    private static HashSet<long> Collect(Document document, long category, ElementFilter? filter)
    {
        var collector = new FilteredElementCollector(document).WhereElementIsNotElementType().OfCategoryId(category.ToElementId());
        if (filter is not null)
        {
            collector = collector.WherePasses(filter);
        }

        return [.. collector.ToElementIds().Select(id => id.ToLong())];
    }

    private static string Classify(SampleDocument document, RuleNode rule, ParameterInfo parameter, HashSet<long> panel, HashSet<long> native)
    {
        var reasons = new List<string>();
        var main = native.Where(id => document.Document.GetElement(id.ToElementId()) is not { } element || Oracle.InMainOrPrimary(element)).ToHashSet();
        if (main.Count != native.Count)
        {
            reasons.Add("design option");
            if (main.SetEquals(panel))
            {
                return string.Join(", ", reasons);
            }
        }

        if (parameter.Kind == ValueKind.Reference && native.Except(panel).Concat(panel.Except(native))
                .All(id => HoldsNone(document.Document, rule.Parameter, id)))
        {
            reasons.Add(NoneReference);
        }

        if ((parameter.Bindings & ParameterBinding.Type) != 0)
        {
            reasons.Add("type parameter");
        }

        if (parameter.Kind == ValueKind.Text && rule.Operator is FilterOperator.Empty or FilterOperator.NotEmpty)
        {
            reasons.Add("empty text");
        }

        if (parameter.Kind == ValueKind.Text && TextRules.IsOrder(rule.Operator))
        {
            reasons.Add("text order");
        }

        if (!document.Key.IsCurrentModel && parameter.Kind == ValueKind.Number)
        {
            reasons.Add("link");
        }

        if (reasons.Count == 0)
        {
            reasons.Add(Unexplained);
        }

        return string.Join(", ", reasons);
    }

    public static string Facts(Document document, ParameterKey key, long id)
    {
        if (document.GetElement(id.ToElementId()) is not { } element)
        {
            return $"{id}: not found";
        }

        var owner = document.GetElement(element.GetTypeId());
        return $"{id}: instance {Describe(element, key)}; type {(owner is null ? "none" : Describe(owner, key))}";
    }

    private static string Describe(Element element, ParameterKey key)
    {
        if (key.BuiltInName is not { } name || !Enum.TryParse<BuiltInParameter>(name, out var builtIn))
        {
            return Oracle.Find(element, key) is { } found ? $"HasValue {found.HasValue}, text '{found.AsValueString()}'" : "no parameter";
        }

        if (element.get_Parameter(builtIn) is not { } parameter)
        {
            return "no parameter";
        }

        var listed = element.Parameters.Cast<Parameter>().Any(item => item.Definition is InternalDefinition definition && definition.BuiltInParameter == builtIn);
        var id = new ElementId(builtIn);
        var hasValue = new ElementParameterFilter(ParameterFilterRuleFactory.CreateHasValueParameterRule(id)).PassesFilter(element);
        var hasNoValue = new ElementParameterFilter(ParameterFilterRuleFactory.CreateHasNoValueParameterRule(id)).PassesFilter(element);
        return $"listed {listed}, HasValue {parameter.HasValue}, text '{parameter.AsValueString()}', rule has value {hasValue}, rule has no value {hasNoValue}";
    }

    private static bool HoldsNone(Document document, ParameterKey key, long id)
    {
        if (document.GetElement(id.ToElementId()) is not { } element)
        {
            return false;
        }

        var (instance, type) = Oracle.Read(element, key);
        var parameter = instance ?? type;
        if (parameter is null && key.BuiltInName is { } name && Enum.TryParse<BuiltInParameter>(name, out var builtIn))
        {
            parameter = element.get_Parameter(builtIn);
        }

        return parameter is { StorageType: StorageType.ElementId } && parameter.AsElementId() == ElementId.InvalidElementId;
    }

    private static string Join(string? before, string text)
    {
        return string.IsNullOrEmpty(before) ? text : before + "; " + text;
    }
}
