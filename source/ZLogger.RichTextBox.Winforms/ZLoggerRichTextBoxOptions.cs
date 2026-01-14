using ZLogger.RichTextBox.Winforms.Themes;
// ReSharper disable ReplaceWithFieldKeyword

namespace ZLogger.RichTextBox.Winforms;

public sealed class ZLoggerRichTextBoxOptions : ZLoggerOptions
{
    private const string DefaultOutputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message}{NewLine}{Exception}";
    private int _maxLogLines = 256;
    private int _spacesPerIndent = 2;
    private int _maxMessageLength = 8 * 1024; // 8KB default

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

    /// <summary>
    /// Maximum message length in bytes before truncation.
    /// Messages exceeding this length will be truncated with "... [truncated]" suffix.
    /// Default: 8KB. Set to 0 to disable truncation (not recommended for large messages).
    /// </summary>
    public int MaxMessageLength
    {
        get => _maxMessageLength;
        set => _maxMessageLength = value switch
        {
            < 0 => 0,
            > 1024 * 1024 => 1024 * 1024, // Cap at 1MB
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
