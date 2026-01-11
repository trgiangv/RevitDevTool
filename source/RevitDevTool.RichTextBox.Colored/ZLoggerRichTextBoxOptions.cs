using RevitDevTool.RichTextBox.Colored.Themes;
using ZLogger;
// ReSharper disable ReplaceWithFieldKeyword

namespace RevitDevTool.RichTextBox.Colored;

public sealed class ZLoggerRichTextBoxOptions : ZLoggerOptions
{
    private const string DefaultOutputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message}{NewLine}{Exception}";
    private int _maxLogLines = 256;
    private int _spacesPerIndent = 2;

    public ZLoggerRichTextBoxOptions()
    {
        Theme = ThemePresets.Literate;
        OutputTemplate = DefaultOutputTemplate;
        FormatProvider = System.Globalization.CultureInfo.InvariantCulture;
    }

    public bool AutoScroll { get; set; } = true;

    public Theme Theme { get; set; }

    public int MaxLogLines
    {
        get => _maxLogLines;
        set => _maxLogLines = value switch
        {
            < 1 => 1,
            > 2048 => 2048,
            _ => value
        };
    }

    public string OutputTemplate { get; set; }

    public IFormatProvider? FormatProvider { get; set; }

    public bool PrettyPrintJson { get; set; }

    public int SpacesPerIndent
    {
        get => _spacesPerIndent;
        set => _spacesPerIndent = value switch
        {
            < 0 => 0,
            > 16 => 16,
            _ => value
        };
    }
}
