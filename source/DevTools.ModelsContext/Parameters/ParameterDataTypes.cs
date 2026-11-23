using DevTools.ModelsContext.Filters;
using System.Globalization;

namespace DevTools.ModelsContext.Parameters;

public static class ParameterDataTypes
{
    public static string? Of(string? specTypeId, string? storage)
    {
        if (specTypeId is null)
        {
            return storage;
        }

        var dash = specTypeId.LastIndexOf('-');
        return dash > specTypeId.IndexOf(':') && specTypeId.Substring(dash + 1).All(c => char.IsDigit(c) || c == '.')
            ? specTypeId.Substring(0, dash)
            : specTypeId;
    }

    public static bool Fits(ParameterKey key, string? specTypeId, string? storage)
    {
        return key.DataType is null
               || (specTypeId is null && storage is null)
               || string.Equals(Of(specTypeId, storage), key.DataType, StringComparison.Ordinal);
    }

    public static bool IsNamedValue(string? text)
    {
        return !string.IsNullOrWhiteSpace(text)
               && !double.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out _)
               && !double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out _);
    }
}
