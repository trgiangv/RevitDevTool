using System.ComponentModel;

namespace DevTools.ModelsContext.Parameters;

public enum ValueKind
{
    Text,
    Integer,
    Number,
    [Description("Yes/No")]
    YesNo,
    [Description("Element")]
    Reference,
    [Description("Enum")]
    Enumerated
}

public enum ParameterStorage
{
    None,
    Integer,
    Double,
    String,
    ElementId
}

public static class ValueKinds
{
    public const string Yes = "Yes";
    public const string No = "No";

    public static ValueKind Infer(ParameterStorage storage, bool isYesNo, bool isMeasurable, bool hasNamedValues)
    {
        if (isYesNo)
        {
            return ValueKind.YesNo;
        }

        if (isMeasurable || storage == ParameterStorage.Double)
        {
            return ValueKind.Number;
        }

        return storage switch
        {
            ParameterStorage.ElementId => ValueKind.Reference,
            ParameterStorage.Integer => hasNamedValues ? ValueKind.Enumerated : ValueKind.Integer,
            _ => ValueKind.Text
        };
    }

    public static bool TryParseYesNo(string text, out int value)
    {
        var trimmed = text.Trim();
        if (string.Equals(trimmed, Yes, StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "true", StringComparison.OrdinalIgnoreCase)
            || trimmed == "1")
        {
            value = 1;
            return true;
        }

        if (string.Equals(trimmed, No, StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "false", StringComparison.OrdinalIgnoreCase)
            || trimmed == "0")
        {
            value = 0;
            return true;
        }

        value = 0;
        return false;
    }

    public static string YesNoText(int value)
    {
        return value == 0 ? No : Yes;
    }
}
