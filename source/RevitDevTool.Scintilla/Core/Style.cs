namespace RevitDevTool.Scintilla.Core;

public readonly struct Style
{
    public Style(Color foreground, Color background, bool bold = false)
    {
        Foreground = foreground;
        Background = background;
        Bold = bold;
    }

    public Color Foreground { get; }
    public Color Background { get; }
    public bool Bold { get; }
}
