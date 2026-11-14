using DevTools.ModelsContext.Model;
using System.Globalization;

namespace RevitDevTool.Tools.ModelsContext;

internal static class ParameterResolver
{
    private const int SpecSampleSize = 500;

    public static ParameterInfo? InfoOf(Document document, ElementId parameterId)
    {
        try
        {
            var value = RevitIds.ValueOf(parameterId);
            if (value < 0)
            {
                var builtIn = (BuiltInParameter)(int)value;
                var label = LabelUtils.GetLabelFor(builtIn);
                return KindOf(document.get_TypeOfStorage(builtIn)) is { } kind
                    ? new ParameterInfo(new ParameterKey(label, builtIn.ToString(), null), label, kind)
                    : null;
            }

            if (document.GetElement(parameterId) is not ParameterElement element)
            {
                return null;
            }

            var definition = element.GetDefinition();
            Guid? shared = element is SharedParameterElement sharedElement ? sharedElement.GuidValue : null;
            return new ParameterInfo(new ParameterKey(definition.Name, null, shared), definition.Name,
                KindOf(definition.GetDataType()));
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static ParameterKey KeyOf(Document document, ElementId parameterId)
    {
        return InfoOf(document, parameterId)?.Key
               ?? new ParameterKey(RevitIds.ValueOf(parameterId).ToString(CultureInfo.InvariantCulture));
    }

    public static ElementId? IdOf(Document document, ParameterKey key)
    {
        if (key.BuiltInName is { } builtInName
            && Enum.TryParse<BuiltInParameter>(builtInName, out var builtIn)
            && builtIn != BuiltInParameter.INVALID)
        {
            return new ElementId(builtIn);
        }

        if (key.SharedId is { } shared)
        {
            return SharedParameterElement.Lookup(document, shared)?.Id;
        }

        return new FilteredElementCollector(document)
            .OfClass(typeof(ParameterElement))
            .Cast<ParameterElement>()
            .FirstOrDefault(element => string.Equals(element.GetDefinition()?.Name, key.Name, StringComparison.Ordinal))
            ?.Id;
    }

    public static Parameter? Find(Element element, ParameterKey key)
    {
        return FindOn(element, key)
               ?? (element.Document.GetElement(element.GetTypeId()) is { } type ? FindOn(type, key) : null);
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
            StorageType.Integer => parameter.AsValueString()
                                   ?? parameter.AsInteger().ToString(CultureInfo.InvariantCulture),
            StorageType.Double => parameter.AsValueString()
                                  ?? parameter.AsDouble().ToString(CultureInfo.InvariantCulture),
            StorageType.ElementId => ElementIdText(document, parameter),
            _ => null
        };
        return string.IsNullOrEmpty(text) ? null : text;
    }

    public static ForgeTypeId? SpecOf(Document document, ElementId parameterId, ParameterKey key,
        ICollection<ElementId> categories)
    {
        try
        {
            if (RevitIds.ValueOf(parameterId) > 0)
            {
                return (document.GetElement(parameterId) as ParameterElement)?.GetDefinition()?.GetDataType();
            }

            foreach (var element in new FilteredElementCollector(document)
                         .WherePasses(ElementReader.ModelFilter(document, categories))
                         .Take(SpecSampleSize))
            {
                if (Find(element, key)?.Definition?.GetDataType() is { } spec)
                {
                    return spec;
                }
            }
        }
        catch (Exception)
        {
        }

        return null;
    }

    public static ValueKind? KindOf(StorageType storage)
    {
        return storage switch
        {
            StorageType.String => ValueKind.Text,
            StorageType.Integer => ValueKind.Integer,
            StorageType.Double => ValueKind.Number,
            StorageType.ElementId => ValueKind.ElementId,
            _ => null
        };
    }

    public static ValueKind KindOf(ForgeTypeId spec)
    {
        try
        {
            if (spec.Equals(SpecTypeId.Boolean.YesNo) || spec.Equals(SpecTypeId.Int.Integer))
            {
                return ValueKind.Integer;
            }

            if (UnitUtils.IsMeasurableSpec(spec))
            {
                return ValueKind.Number;
            }

            if (Category.IsBuiltInCategory(spec))
            {
                return ValueKind.ElementId;
            }
        }
        catch (Exception)
        {
        }

        return ValueKind.Text;
    }

    private static Parameter? FindOn(Element element, ParameterKey key)
    {
        if (key.BuiltInName is { } builtInName && Enum.TryParse<BuiltInParameter>(builtInName, out var builtIn))
        {
            return element.get_Parameter(builtIn);
        }

        return key.SharedId is { } shared ? element.get_Parameter(shared) : element.LookupParameter(key.Name);
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
               ?? RevitIds.ValueOf(id).ToString(CultureInfo.InvariantCulture);
    }
}
