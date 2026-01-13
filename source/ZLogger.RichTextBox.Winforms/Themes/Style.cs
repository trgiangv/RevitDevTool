namespace ZLogger.RichTextBox.Winforms.Themes;

public readonly struct Style(Color foreground, Color background)
{
    public Color Background { get; } = background;

    public Color Foreground { get; } = foreground;
}