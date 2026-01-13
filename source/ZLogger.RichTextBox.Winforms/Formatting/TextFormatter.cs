using Cysharp.Text;

namespace ZLogger.RichTextBox.Winforms.Formatting;

public static class TextFormatter
{
    public static string Format(string value, string? format)
    {
        if (string.IsNullOrEmpty(format) || string.IsNullOrEmpty(value))
        {
            return value;
        }

        var first = format![0];
        return first switch
        {
            'u' => value.ToUpperInvariant(),
            'w' => value.ToLowerInvariant(),
            't' => FormatTitleCase(value),
            _ => value
        };
    }

    private static string FormatTitleCase(string value)
    {
        using var builder = ZString.CreateStringBuilder();
        builder.Append(char.ToUpperInvariant(value[0]));

        if (value.Length > 1)
        {
            builder.Append(value.AsSpan(1).ToString().ToLowerInvariant());
        }

        return builder.ToString();
    }
}