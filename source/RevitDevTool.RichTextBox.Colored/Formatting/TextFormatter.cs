using System.Text;

namespace RevitDevTool.RichTextBox.Colored.Formatting;

public static class TextFormatter
{
    private static readonly StringBuilder FormatterBuilder = new();

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
        FormatterBuilder.Clear();
        FormatterBuilder.Append(char.ToUpperInvariant(value[0]));

        if (value.Length > 1)
        {
            FormatterBuilder.Append(value.Substring(1).ToLowerInvariant());
        }

        return FormatterBuilder.ToString();
    }
}