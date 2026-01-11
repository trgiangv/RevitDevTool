namespace RevitDevTool.RichTextBox.Colored.Themes;

public readonly struct Style
{
    public Style(Color foreground, Color background)
    {
        Background = background;
        Foreground = foreground;
    }

    public Color Background { get; }

    public Color Foreground { get; }
}