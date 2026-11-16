using DevTools.ModelsContext.Diagnostics;
using DevTools.ModelsContext.Model;
using Nice3point.Revit.Extensions;
using System.Globalization;

namespace RevitDevTool.Tools.ModelsContext;

internal sealed record ResolvedParameter(ElementId Id, ParameterKey Key, StorageType Storage, ForgeTypeId? Spec)
{
    public bool IsYesNo => ParameterResolver.IsYesNo(Spec);
}

internal static class ParameterResolver
{
    public static ParameterStorage StorageOf(StorageType storage)
    {
        return storage switch
        {
            StorageType.Integer => ParameterStorage.Integer,
            StorageType.Double => ParameterStorage.Double,
            StorageType.String => ParameterStorage.String,
            StorageType.ElementId => ParameterStorage.ElementId,
            _ => ParameterStorage.None
        };
    }

    public static ForgeTypeId? SpecOf(Definition? definition)
    {
        try
        {
            return definition?.GetDataType() is { } spec && !string.IsNullOrEmpty(spec.TypeId) ? spec : null;
        }
        catch (Exception exception)
        {
            RevitProfiler.Logger.Swallowed(exception, "Parameter spec", definition?.Name);
            return null;
        }
    }

    public static string? DataTypeOf(ForgeTypeId? spec, StorageType? storage)
    {
        if (spec is null)
        {
            return storage?.ToString();
        }

        var typeId = spec.TypeId;
        var dash = typeId.LastIndexOf('-');
        return dash > typeId.IndexOf(':') && typeId.Substring(dash + 1).All(c => char.IsDigit(c) || c == '.')
            ? typeId.Substring(0, dash)
            : typeId;
    }

    public static bool IsYesNo(ForgeTypeId? spec)
    {
        return spec is not null && spec.Equals(SpecTypeId.Boolean.YesNo);
    }

    public static bool IsMeasurable(ForgeTypeId? spec)
    {
        try
        {
            return spec is not null && UnitUtils.IsMeasurableSpec(spec);
        }
        catch (Exception exception)
        {
            RevitProfiler.Logger.Swallowed(exception, "Measurable spec", spec?.TypeId);
            return false;
        }
    }

    public static string? SpecLabel(ForgeTypeId? spec)
    {
        try
        {
            return spec is null ? null : LabelUtils.GetLabelForSpec(spec);
        }
        catch (Exception exception)
        {
            RevitProfiler.Logger.Swallowed(exception, "Spec label", spec?.TypeId);
            return null;
        }
    }

    public static bool IsNamedValue(string? text)
    {
        return !string.IsNullOrWhiteSpace(text)
               && !double.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out _)
               && !double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out _);
    }

    public static ParameterKey KeyOf(Document document, ElementId parameterId)
    {
        try
        {
            var value = parameterId.ToLong();
            if (value < 0)
            {
                var builtIn = (BuiltInParameter)(int)value;
                return ParameterKey.BuiltIn(LabelUtils.GetLabelFor(builtIn), builtIn.ToString());
            }

            if (document.GetElement(parameterId) is ParameterElement element)
            {
                var definition = element.GetDefinition();
                return element is SharedParameterElement shared
                    ? ParameterKey.Shared(definition.Name, shared.GuidValue)
                    : ParameterKey.Named(definition.Name, DataTypeOf(SpecOf(definition), null));
            }
        }
        catch (Exception exception)
        {
            RevitProfiler.Logger.Swallowed(exception, "Parameter key", parameterId);
        }

        return new ParameterKey(parameterId.ToString());
    }

    public static IReadOnlyCollection<long> IdsOf(
        Document document,
        ParameterKey key,
        ICollection<ElementId> categories,
        IReadOnlyCollection<long>? known)
    {
        if (key.BuiltInName is { } builtInName)
        {
            return Enum.TryParse<BuiltInParameter>(builtInName, out var builtIn) && builtIn != BuiltInParameter.INVALID
                ? [new ElementId(builtIn).ToLong()]
                : [];
        }

        if (key.SharedId is { } shared)
        {
            return SharedParameterElement.Lookup(document, shared) is { } element ? [element.Id.ToLong()] : [];
        }

        return known ?? NamedIds(document, key, categories);
    }

    public static IReadOnlyList<ResolvedParameter> Resolve(
        Document document,
        ParameterKey key,
        ICollection<ElementId> categories,
        IReadOnlyCollection<long>? known = null)
    {
        var resolved = new List<ResolvedParameter>();
        try
        {
            foreach (var value in IdsOf(document, key, categories, known))
            {
                var id = value.ToElementId();
                var holds = new LogicalOrFilter(
                    new ElementParameterFilter(ParameterFilterRuleFactory.CreateHasValueParameterRule(id)),
                    new ElementParameterFilter(ParameterFilterRuleFactory.CreateHasNoValueParameterRule(id)));
                var element = new FilteredElementCollector(document)
                    .WherePasses(ElementReader.ModelFilter(document, categories))
                    .WherePasses(holds)
                    .FirstElement();
                if (element is not null && Find(element, key, id) is { } parameter)
                {
                    resolved.Add(new ResolvedParameter(id, key, parameter.StorageType, SpecOf(parameter.Definition)));
                }
            }
        }
        catch (Exception exception)
        {
            RevitProfiler.Logger.Swallowed(exception, "Resolve parameter", key.Name);
        }

        return resolved;
    }

    public static bool Matches(ParameterKey key, Parameter parameter)
    {
        return key.BuiltInName is null && key.SharedId is null && !parameter.IsShared
               && parameter.Id.ToLong() > 0
               && string.Equals(parameter.Definition?.Name, key.Name, StringComparison.Ordinal)
               && Fits(key, SpecOf(parameter.Definition), parameter.StorageType);
    }

    private static HashSet<long> NamedIds(Document document, ParameterKey key, ICollection<ElementId> categories)
    {
        var ids = new HashSet<long>(new FilteredElementCollector(document)
            .OfClass(typeof(ParameterElement))
            .Cast<ParameterElement>()
            .Where(element => element is not SharedParameterElement
                              && element.GetDefinition() is { } definition
                              && string.Equals(definition.Name, key.Name, StringComparison.Ordinal)
                              && Fits(key, SpecOf(definition), null))
            .Select(element => element.Id.ToLong()));
        var sampled = new HashSet<long>();
        foreach (var element in new FilteredElementCollector(document).WherePasses(ElementReader.ModelFilter(document, categories)))
        {
            var typeId = element.GetTypeId();
            if (!sampled.Add(typeId != ElementId.InvalidElementId ? typeId.ToLong() : element.Category?.Id.ToLong() ?? 0))
            {
                continue;
            }

            AddNamed(ids, element, key);
            if (document.GetElement(typeId) is { } type)
            {
                AddNamed(ids, type, key);
            }
        }

        return ids;
    }

    private static void AddNamed(HashSet<long> ids, Element element, ParameterKey key)
    {
        foreach (var parameter in element.GetParameters(key.Name))
        {
            if (Matches(key, parameter))
            {
                ids.Add(parameter.Id.ToLong());
            }
        }
    }

    public static Parameter? Find(Element element, ParameterKey key, ElementId? id = null)
    {
        return FindOn(element, key, id)
               ?? (element.Document.GetElement(element.GetTypeId()) is { } type ? FindOn(type, key, id) : null);
    }

    public static string? ValueText(Document document, Parameter parameter)
    {
        if (!parameter.HasValue)
        {
            return null;
        }

        var text = parameter.StorageType switch
        {
            StorageType.String => parameter.AsString(),
            StorageType.Integer when IsYesNo(SpecOf(parameter.Definition)) => ValueKinds.YesNoText(parameter.AsInteger()),
            StorageType.Integer => parameter.AsValueString()
                                   ?? parameter.AsInteger().ToString(CultureInfo.InvariantCulture),
            StorageType.Double => parameter.AsValueString()
                                  ?? parameter.AsDouble().ToString(CultureInfo.InvariantCulture),
            StorageType.ElementId => ElementIdText(document, parameter),
            _ => null
        };
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private static Parameter? FindOn(Element element, ParameterKey key, ElementId? id)
    {
        if (key.BuiltInName is { } builtInName)
        {
            return Enum.TryParse<BuiltInParameter>(builtInName, out var builtIn) ? element.get_Parameter(builtIn) : null;
        }

        if (key.SharedId is { } shared)
        {
            return element.get_Parameter(shared);
        }

        foreach (var parameter in element.GetParameters(key.Name))
        {
            if (id is not null
                    ? parameter.Id == id
                    : !parameter.IsShared && Fits(key, SpecOf(parameter.Definition), parameter.StorageType))
            {
                return parameter;
            }
        }

        return null;
    }

    private static bool Fits(ParameterKey key, ForgeTypeId? spec, StorageType? storage)
    {
        return key.DataType is null
               || (spec is null && storage is null)
               || string.Equals(DataTypeOf(spec, storage), key.DataType, StringComparison.Ordinal);
    }

    private static string? ElementIdText(Document document, Parameter parameter)
    {
        var id = parameter.AsElementId();
        if (id == ElementId.InvalidElementId)
        {
            return null;
        }

        return document.GetElement(id)?.Name
               ?? parameter.AsValueString()
               ?? id.ToString();
    }
}
