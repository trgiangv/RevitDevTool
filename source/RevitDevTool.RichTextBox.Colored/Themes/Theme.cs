using RevitDevTool.RichTextBox.Colored.Rtf;

namespace RevitDevTool.RichTextBox.Colored.Themes;

public class Theme
{
    private readonly Dictionary<StyleToken, Style> _styles;

    public Theme(Style defaultStyle, Dictionary<StyleToken, Style> styles)
    {
        DefaultStyle = defaultStyle;
        _styles = styles;
    }

    public Style DefaultStyle { get; }

    public IEnumerable<Color> Colors
    {
        get
        {
            yield return DefaultStyle.Foreground;
            yield return DefaultStyle.Background;

            foreach (var style in _styles.Values)
            {
                yield return style.Foreground;
                yield return style.Background;
            }
        }
    }

    public void Render(IRtfCanvas canvas, StyleToken styleToken, string value)
    {
        var themeStyle = _styles[styleToken];

        canvas.SelectionStart = canvas.TextLength;
        canvas.SelectionLength = 0;
        canvas.SelectionColor = themeStyle.Foreground;
        canvas.SelectionBackColor = themeStyle.Background;
        canvas.AppendText(value);

        canvas.SelectionColor = DefaultStyle.Foreground;
        canvas.SelectionBackColor = DefaultStyle.Background;
    }
}