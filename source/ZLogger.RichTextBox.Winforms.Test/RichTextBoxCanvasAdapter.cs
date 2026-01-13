using ZLogger.RichTextBox.Winforms.Rtf;
namespace ZLogger.RichTextBox.Winforms.Test;

public sealed class RichTextBoxCanvasAdapter(System.Windows.Forms.RichTextBox richTextBox) : IRtfCanvas
{
    private readonly System.Windows.Forms.RichTextBox _richTextBox = richTextBox ?? throw new ArgumentNullException(nameof(richTextBox));

    public int TextLength => _richTextBox.TextLength;

    public int SelectionStart
    {
        get => _richTextBox.SelectionStart;
        set => _richTextBox.SelectionStart = value;
    }

    public int SelectionLength
    {
        get => _richTextBox.SelectionLength;
        set => _richTextBox.SelectionLength = value;
    }

    public Color SelectionColor
    {
        get => _richTextBox.SelectionColor;
        set => _richTextBox.SelectionColor = value;
    }

    public Color SelectionBackColor
    {
        get => _richTextBox.SelectionBackColor;
        set => _richTextBox.SelectionBackColor = value;
    }

    public void AppendText(string text) => _richTextBox.AppendText(text);
}
