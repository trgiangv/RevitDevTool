using DevTools.ModelsContext.Engine;
using DevTools.ModelsContext.Model;
using Nice3point.Revit.Extensions;
using System.Globalization;
using RevitRule = Autodesk.Revit.DB.FilterRule;
using RuleNode = DevTools.ModelsContext.Model.FilterRule;

namespace RevitDevTool.ModelsContext.LiveTests;

internal sealed record NativeOutcome(int? Count, string Accepted, int? ViewFilterCount, string Reason, string Note);

internal sealed class NotExpressibleException(string reason, string message) : Exception(message)
{
    public string Reason { get; } = reason;
}

internal static class NativeFilter
{
    public const string Yes = "yes";
    public const string No = "no";
    public const string NotApplicable = "not applicable";
    public const string Inexpressible = "not expressible";
    public const string MissingParameter = "missing parameter";

    private const double Epsilon = 1e-6;

    public static NativeOutcome Run(FilterDefinition definition, IReadOnlyList<SampleDocument> documents, ScopeKind scope)
    {
        var known = SampleModel.KnownSources;
        var total = 0;
        var accepted = NotApplicable;
        int? viewCount = null;
        var notes = new List<string>();
        foreach (var document in documents)
        {
            if (FilterScopes.ForDocument(definition.Root, document.Source, known) is not { } root)
            {
                continue;
            }

            try
            {
                var categories = definition.Groups.Count > 0 ? definition.Groups.ToList() : Oracle.ModelCategories(document.Document).ToList();
                var filter = Compile(document.Document, root, definition.Groups);
                total += Collect(document, categories, filter, scope).Count;
                if (document.Key.IsCurrentModel)
                {
                    var check = AsViewFilter(document, categories, definition.Groups.Count > 0, filter, scope);
                    accepted = check.Accepted;
                    viewCount = check.Count;
                    if (check.Note.Length > 0)
                    {
                        notes.Add(check.Note);
                    }
                }
            }
            catch (NotExpressibleException exception)
            {
                return new NativeOutcome(null, accepted, null, exception.Reason, $"{document.Label}: {exception.Message}");
            }
            catch (Exception exception)
            {
                return new NativeOutcome(null, accepted, null, Inexpressible, $"{document.Label}: Revit refused the filter: {exception.Message}");
            }
        }

        return new NativeOutcome(total, accepted, viewCount, string.Empty, string.Join("; ", notes));
    }

    private static HashSet<long> Collect(SampleDocument document, List<long> categories, ElementFilter? filter, ScopeKind scope)
    {
        var collector = new FilteredElementCollector(document.Document).WhereElementIsNotElementType().WherePasses(Multicategory(categories));
        if (filter is not null)
        {
            collector = collector.WherePasses(filter);
        }

        var ids = new HashSet<long>(collector.ToElementIds().Select(id => id.ToLong()));
        if (scope == ScopeKind.ActiveView)
        {
            ids.IntersectWith(Oracle.ViewIds(document));
        }

        return ids;
    }

    private static ElementMulticategoryFilter Multicategory(IEnumerable<long> categories)
    {
        return new ElementMulticategoryFilter(categories.Select(id => id.ToElementId()).ToList());
    }

    private static (string Accepted, int? Count, string Note) AsViewFilter(SampleDocument document, List<long> categories, bool explicitCategories, ElementFilter? filter, ScopeKind scope)
    {
        var doc = document.Document;
        try
        {
            var filterable = new HashSet<long>(ParameterFilterUtilities.GetAllFilterableCategories().Select(id => id.ToLong()));
            var usable = categories.Where(filterable.Contains).ToList();
            if (usable.Count == 0 || (explicitCategories && usable.Count != categories.Count))
            {
                return (No, null, "a checked category cannot take a view filter");
            }

            var ids = new HashSet<ElementId>(usable.Select(id => id.ToElementId()));
            if (filter is not null && !ParameterFilterElement.ElementFilterIsAcceptableForParameterFilterElement(doc, ids, filter))
            {
                return (No, null, "Revit does not accept this rule tree as a view filter");
            }

            return RolledBack(doc, () =>
            {
                var name = $"NF check {Guid.NewGuid():N}";
                var element = filter is null
                    ? ParameterFilterElement.Create(doc, name, ids)
                    : ParameterFilterElement.Create(doc, name, ids, filter);
                var collector = new FilteredElementCollector(doc).WhereElementIsNotElementType().WherePasses(Multicategory(usable));
                if (element.GetElementFilter() is { } created)
                {
                    collector = collector.WherePasses(created);
                }

                var found = new HashSet<long>(collector.ToElementIds().Select(id => id.ToLong()));
                if (scope == ScopeKind.ActiveView)
                {
                    found.IntersectWith(Oracle.ViewIds(document));
                }

                return (Yes, (int?)found.Count, string.Empty);
            });
        }
        catch (Exception exception)
        {
            return (No, null, "Revit refused to create the view filter: " + exception.Message);
        }
    }

    private static T RolledBack<T>(Document document, Func<T> work)
    {
        if (document.IsModifiable)
        {
            using var sub = new SubTransaction(document);
            sub.Start();
            try
            {
                return work();
            }
            finally
            {
                if (sub.HasStarted())
                {
                    sub.RollBack();
                }
            }
        }

        using var transaction = new Transaction(document, "Model Context native check");
        transaction.Start();
        try
        {
            return work();
        }
        finally
        {
            if (transaction.HasStarted())
            {
                transaction.RollBack();
            }
        }
    }

    internal static ElementFilter? Compile(Document document, FilterNode node, IReadOnlyList<long> groups)
    {
        return node switch
        {
            FilterSet set => Set(document, set, groups),
            RuleNode rule => Leaf(document, rule, groups),
            _ => throw new NotExpressibleException(Inexpressible, "a rule read from a Revit filter has no counterpart")
        };
    }

    private static ElementFilter? Set(Document document, FilterSet set, IReadOnlyList<long> groups)
    {
        var all = set.Match == SetMatch.All;
        var parts = new List<ElementFilter>();
        var everything = set.Children.Count == 0;
        foreach (var child in set.Children)
        {
            if (Compile(document, child, groups) is { } part)
            {
                parts.Add(part);
            }
            else if (!all)
            {
                everything = true;
            }
        }

        ElementFilter? body = !all && everything || parts.Count == 0
            ? null
            : parts.Count == 1
                ? parts[0]
                : all ? new LogicalAndFilter(parts) : new LogicalOrFilter(parts);
        if (set.Scope is not { Categories.Count: > 0 } scope)
        {
            return body;
        }

        var categories = Multicategory(scope.Categories);
        return body is null ? categories : new LogicalAndFilter([categories, body]);
    }

    private static ElementFilter Leaf(Document document, RuleNode rule, IReadOnlyList<long> groups)
    {
        var parameter = Resolve(document, rule.Parameter);
        var id = parameter.Id;
        switch (rule.Operator)
        {
            case FilterOperator.NotEmpty:
                return new ElementParameterFilter(ParameterFilterRuleFactory.CreateHasValueParameterRule(id));
            case FilterOperator.Empty:
                return new ElementParameterFilter(ParameterFilterRuleFactory.CreateHasNoValueParameterRule(id));
            case FilterOperator.Defined:
                return new LogicalOrFilter([
                    new ElementParameterFilter(ParameterFilterRuleFactory.CreateHasValueParameterRule(id)),
                    new ElementParameterFilter(ParameterFilterRuleFactory.CreateHasNoValueParameterRule(id))]);
            case FilterOperator.Undefined:
                return new LogicalAndFilter([
                    new ElementParameterFilter(ParameterFilterRuleFactory.CreateHasValueParameterRule(id), true),
                    new ElementParameterFilter(ParameterFilterRuleFactory.CreateHasNoValueParameterRule(id), true)]);
        }

        return new ElementParameterFilter(parameter.Storage switch
        {
            StorageType.String => TextRule(id, rule.Operator, rule.Value),
            StorageType.Integer => IntegerRule(document, rule, parameter, groups),
            StorageType.Double => DoubleRule(document, rule, parameter, groups),
            StorageType.ElementId => ReferenceRule(document, rule, parameter, groups),
            _ => throw new NotExpressibleException(Inexpressible, $"{rule.Parameter.Name} has no value storage a rule can compare")
        });
    }

    private static RevitRule TextRule(ElementId id, FilterOperator op, string text)
    {
        return op switch
        {
            FilterOperator.Equal => ParameterFilterRuleFactory.CreateEqualsRule(id, text),
            FilterOperator.NotEqual => ParameterFilterRuleFactory.CreateNotEqualsRule(id, text),
            FilterOperator.Greater => ParameterFilterRuleFactory.CreateGreaterRule(id, text),
            FilterOperator.GreaterOrEqual => ParameterFilterRuleFactory.CreateGreaterOrEqualRule(id, text),
            FilterOperator.Less => ParameterFilterRuleFactory.CreateLessRule(id, text),
            FilterOperator.LessOrEqual => ParameterFilterRuleFactory.CreateLessOrEqualRule(id, text),
            FilterOperator.Contains => ParameterFilterRuleFactory.CreateContainsRule(id, text),
            FilterOperator.NotContains => ParameterFilterRuleFactory.CreateNotContainsRule(id, text),
            FilterOperator.BeginsWith => ParameterFilterRuleFactory.CreateBeginsWithRule(id, text),
            FilterOperator.NotBeginsWith => ParameterFilterRuleFactory.CreateNotBeginsWithRule(id, text),
            FilterOperator.EndsWith => ParameterFilterRuleFactory.CreateEndsWithRule(id, text),
            FilterOperator.NotEndsWith => ParameterFilterRuleFactory.CreateNotEndsWithRule(id, text),
            _ => throw new NotExpressibleException(Inexpressible, $"{op} is not a text rule")
        };
    }

    private static RevitRule IntegerRule(Document document, RuleNode rule, Resolved parameter, IReadOnlyList<long> groups)
    {
        var text = rule.Value.Trim();
        int number;
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            number = parsed;
        }
        else if (ValueKinds.TryParseYesNo(text, out var flag))
        {
            number = flag;
        }
        else
        {
            number = NamedValue(document, rule.Parameter, groups, text, value => value.AsInteger());
        }

        var id = parameter.Id;
        return rule.Operator switch
        {
            FilterOperator.Equal => ParameterFilterRuleFactory.CreateEqualsRule(id, number),
            FilterOperator.NotEqual => ParameterFilterRuleFactory.CreateNotEqualsRule(id, number),
            FilterOperator.Greater => ParameterFilterRuleFactory.CreateGreaterRule(id, number),
            FilterOperator.GreaterOrEqual => ParameterFilterRuleFactory.CreateGreaterOrEqualRule(id, number),
            FilterOperator.Less => ParameterFilterRuleFactory.CreateLessRule(id, number),
            FilterOperator.LessOrEqual => ParameterFilterRuleFactory.CreateLessOrEqualRule(id, number),
            _ => throw new NotExpressibleException(Inexpressible, $"{rule.Operator} is a text rule, {rule.Parameter.Name} is a number")
        };
    }

    private static RevitRule DoubleRule(Document document, RuleNode rule, Resolved parameter, IReadOnlyList<long> groups)
    {
        double target;
        var epsilon = Epsilon;
        if (rule.Picked is { } picked)
        {
            target = picked.Middle;
            epsilon = picked.HalfSpan + Epsilon;
        }
        else if ((parameter.Spec ?? SampledSpec(document, rule.Parameter, groups)) is { } spec && UnitUtils.IsMeasurableSpec(spec))
        {
            var units = document.GetUnits();
            if (!UnitFormatUtils.TryParse(units, spec, rule.Value, out double measured))
            {
                throw new NotExpressibleException(Inexpressible, $"'{rule.Value}' does not parse as a {rule.Parameter.Name} value");
            }

            target = measured;
            epsilon = Math.Max(Epsilon, Oracle.HalfStep(units, spec));
        }
        else if (!double.TryParse(rule.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out target))
        {
            throw new NotExpressibleException(Inexpressible, $"'{rule.Value}' is not a number");
        }

        var id = parameter.Id;
        return rule.Operator switch
        {
            FilterOperator.Equal => ParameterFilterRuleFactory.CreateEqualsRule(id, target, epsilon),
            FilterOperator.NotEqual => ParameterFilterRuleFactory.CreateNotEqualsRule(id, target, epsilon),
            FilterOperator.Greater => ParameterFilterRuleFactory.CreateGreaterRule(id, target, epsilon),
            FilterOperator.GreaterOrEqual => ParameterFilterRuleFactory.CreateGreaterOrEqualRule(id, target, epsilon),
            FilterOperator.Less => ParameterFilterRuleFactory.CreateLessRule(id, target, epsilon),
            FilterOperator.LessOrEqual => ParameterFilterRuleFactory.CreateLessOrEqualRule(id, target, epsilon),
            _ => throw new NotExpressibleException(Inexpressible, $"{rule.Operator} is a text rule, {rule.Parameter.Name} is a number")
        };
    }

    private static RevitRule ReferenceRule(Document document, RuleNode rule, Resolved parameter, IReadOnlyList<long> groups)
    {
        var text = rule.Value.Trim();
        var target = long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? number.ToElementId()
            : NamedValue(document, rule.Parameter, groups, text, value => value.AsElementId());
        return rule.Operator switch
        {
            FilterOperator.Equal => ParameterFilterRuleFactory.CreateEqualsRule(parameter.Id, target),
            FilterOperator.NotEqual => ParameterFilterRuleFactory.CreateNotEqualsRule(parameter.Id, target),
            _ => throw new NotExpressibleException(Inexpressible, $"{rule.Operator} does not apply to an element reference")
        };
    }

    private static T NamedValue<T>(Document document, ParameterKey key, IReadOnlyList<long> groups, string text, Func<Parameter, T> read)
    {
        foreach (var element in Oracle.Candidates(document, groups, true))
        {
            var (instance, type) = Oracle.Read(element, key);
            foreach (var parameter in new[] { instance, type })
            {
                if (parameter is { HasValue: true } && string.Equals(parameter.AsValueString(), text, StringComparison.OrdinalIgnoreCase))
                {
                    return read(parameter);
                }
            }
        }

        throw new NotExpressibleException(Inexpressible, $"'{text}' is not in the value list of {key.Name}");
    }

    private static Resolved Resolve(Document document, ParameterKey key)
    {
        if (key.BuiltInName is { } name)
        {
            var builtIn = (BuiltInParameter)Enum.Parse(typeof(BuiltInParameter), name);
            return new Resolved(new ElementId(builtIn), document.get_TypeOfStorage(builtIn), SpecOf(key));
        }

        if (key.SharedId is { } guid)
        {
            return Of(SharedParameterElement.Lookup(document, guid)
                      ?? throw new NotExpressibleException(MissingParameter, $"{key.Name} is not in this document"));
        }

        var found = new FilteredElementCollector(document).OfClass(typeof(ParameterElement)).Cast<ParameterElement>()
            .FirstOrDefault(element => element is not SharedParameterElement
                                       && element.GetDefinition() is { } definition
                                       && definition.Name == key.Name
                                       && (key.DataType is null || Oracle.WithoutVersion(definition.GetDataType().TypeId) == key.DataType));
        return found is null
            ? throw new NotExpressibleException(Inexpressible, $"{key.Name} is not in the filter parameter list (a family parameter)")
            : Of(found);
    }

    private static Resolved Of(ParameterElement element)
    {
        var spec = element.GetDefinition().GetDataType();
        return new Resolved(element.Id, StorageOf(spec), spec);
    }

    private static ForgeTypeId? SpecOf(ParameterKey key)
    {
        return key.DataType is { } dataType && dataType.StartsWith("autodesk.spec", StringComparison.Ordinal)
            ? new ForgeTypeId(dataType)
            : null;
    }

    private static ForgeTypeId? SampledSpec(Document document, ParameterKey key, IReadOnlyList<long> groups)
    {
        foreach (var element in Oracle.Candidates(document, groups, true))
        {
            var (instance, type) = Oracle.Read(element, key);
            if ((instance ?? type)?.Definition.GetDataType() is { } spec && !string.IsNullOrEmpty(spec.TypeId))
            {
                return spec;
            }
        }

        return null;
    }

    private static StorageType StorageOf(ForgeTypeId spec)
    {
        if (string.IsNullOrEmpty(spec.TypeId))
        {
            throw new NotExpressibleException(Inexpressible, "the parameter has no data type");
        }

        if (UnitUtils.IsMeasurableSpec(spec) || Is(spec, SpecTypeId.Number))
        {
            return StorageType.Double;
        }

        if (Is(spec, SpecTypeId.Boolean.YesNo) || Is(spec, SpecTypeId.Int.Integer))
        {
            return StorageType.Integer;
        }

        return Is(spec, SpecTypeId.String.Text) || Is(spec, SpecTypeId.String.MultilineText) || Is(spec, SpecTypeId.String.Url)
            ? StorageType.String
            : StorageType.ElementId;
    }

    private static bool Is(ForgeTypeId spec, ForgeTypeId other)
    {
        return Oracle.WithoutVersion(spec.TypeId) == Oracle.WithoutVersion(other.TypeId);
    }

    private sealed record Resolved(ElementId Id, StorageType Storage, ForgeTypeId? Spec);
}
