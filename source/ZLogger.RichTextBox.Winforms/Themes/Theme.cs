using ZLogger.RichTextBox.Winforms.Rtf;

namespace ZLogger.RichTextBox.Winforms.Themes;

public class Theme(Style defaultStyle, Dictionary<StyleToken, Style> styles)
{
    public Style DefaultStyle { get; } = defaultStyle;

    public IEnumerable<Color> Colors
    {
        get
        {
            yield return DefaultStyle.Foreground;
            yield return DefaultStyle.Background;

            foreach (var style in styles.Values)
            {
                yield return style.Foreground;
                yield return style.Background;
            }
        }
    }

    public void Render(IRtfCanvas canvas, StyleToken styleToken, string value)
    {
        var themeStyle = styles[styleToken];

        canvas.SelectionStart = canvas.TextLength;
        canvas.SelectionLength = 0;
        canvas.SelectionColor = themeStyle.Foreground;
        canvas.SelectionBackColor = themeStyle.Background;
        canvas.AppendText(value);

        canvas.SelectionColor = DefaultStyle.Foreground;
        canvas.SelectionBackColor = DefaultStyle.Background;
    }
}