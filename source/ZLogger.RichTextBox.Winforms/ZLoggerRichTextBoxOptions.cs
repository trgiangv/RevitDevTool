using ZLogger.RichTextBox.Winforms.Themes;
// ReSharper disable ReplaceWithFieldKeyword

namespace ZLogger.RichTextBox.Winforms;

public sealed class ZLoggerRichTextBoxOptions : ZLoggerOptions
{
    private const string DefaultOutputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message}{NewLine}{Exception}";
    private int _maxLogLines = 256;
    private int _spacesPerIndent = 2;

    public bool AutoScroll { get; set; } = true;

    public Theme Theme { get; set; } = ThemePresets.Literate;

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

    public string OutputTemplate { get; set; } = DefaultOutputTemplate;

    public IFormatProvider? FormatProvider { get; set; } = System.Globalization.CultureInfo.InvariantCulture;

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
