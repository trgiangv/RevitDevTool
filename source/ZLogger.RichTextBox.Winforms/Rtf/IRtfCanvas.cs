namespace ZLogger.RichTextBox.Winforms.Rtf;

public interface IRtfCanvas
{
    int TextLength { get; }

    int SelectionStart { get; set; }
    int SelectionLength { get; set; }

    Color SelectionColor { get; set; }
    Color SelectionBackColor { get; set; }

    void AppendText(string text);
}