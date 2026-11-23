using DevTools.ModelsContext.Parameters;
using System.Globalization;

namespace RevitDevTool.Tools.ModelsContext.Parameters;

internal static class ParameterValues
{
    public static string? ValueText(Document document, Parameter parameter)
    {
        if (!parameter.HasValue)
        {
            return null;
        }

        var text = parameter.StorageType switch
        {
            StorageType.String => parameter.AsString(),
            StorageType.Integer when SpecInfo.IsYesNo(SpecInfo.SpecOf(parameter.Definition)) => ValueKinds.YesNoText(parameter.AsInteger()),
            StorageType.Integer => parameter.AsValueString()
                                   ?? parameter.AsInteger().ToString(CultureInfo.InvariantCulture),
            StorageType.Double => parameter.AsValueString()
                                  ?? parameter.AsDouble().ToString(CultureInfo.InvariantCulture),
            StorageType.ElementId => ElementIdText(document, parameter),
            _ => null
        };
        return string.IsNullOrEmpty(text) ? null : text;
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
