using Cysharp.Text;
using ZLogger.RichTextBox.Winforms.Themes;

namespace ZLogger.RichTextBox.Winforms.Rtf;

public sealed class RtfBuilder : IRtfCanvas, IDisposable
{
    private Utf16ValueStringBuilder _body;
    private Utf16ValueStringBuilder _documentBuilder;
    private readonly Dictionary<Color, int> _colorTable = new();
    private int _currentFgIndex;
    private int _currentBgIndex;

    public RtfBuilder(Theme theme)
    {
        _body = ZString.CreateStringBuilder();
        _documentBuilder = ZString.CreateStringBuilder();

        SelectionColor = theme.DefaultStyle.Foreground;
        SelectionBackColor = theme.DefaultStyle.Background;
        _currentFgIndex = RegisterColor(SelectionColor);
        _currentBgIndex = RegisterColor(SelectionBackColor);

        foreach (var color in theme.Colors)
        {
            RegisterColor(color);
        }
    }

    public int TextLength { get; private set; }

    public int SelectionStart { get; set; }

    public int SelectionLength { get; set; }

    public Color SelectionColor { get; set; }

    public Color SelectionBackColor { get; set; }

    public void AppendText(string text)
    {
        EnsureColorSwitch();
        EscapeAndAppend(text);
        TextLength += text.Length;
    }

    public string Rtf => BuildDocument();

    private void EnsureColorSwitch()
    {
        var fgIdx = RegisterColor(SelectionColor);
        var bgIdx = RegisterColor(SelectionBackColor);

        if (fgIdx == _currentFgIndex && bgIdx == _currentBgIndex)
        {
            return;
        }

        if (fgIdx != _currentFgIndex)
        {
            _currentFgIndex = fgIdx;
            _body.Append(@"\cf");
            _body.Append(fgIdx);
            _body.Append(' ');
        }

        if (bgIdx != _currentBgIndex)
        {
            _currentBgIndex = bgIdx;
            _body.Append(@"\highlight");
            _body.Append(bgIdx);
            _body.Append(' ');
        }
    }

    private int RegisterColor(Color color)
    {
        if (_colorTable.TryGetValue(color, out var idx))
        {
            return idx;
        }

        idx = _colorTable.Count + 1;
        _colorTable.Add(color, idx);
        return idx;
    }

    private void EscapeAndAppend(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        var segmentStart = 0;
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (!NeedsEscaping(ch))
            {
                continue;
            }

            AppendSegment(value, segmentStart, i);
            AppendEscapedChar(ch);
            segmentStart = i + 1;
        }

        AppendSegment(value, segmentStart, value.Length);
    }

    private static bool NeedsEscaping(char ch)
    {
        if (ch > 0x7f)
        {
            return true;
        }

        return ch is '\\' or '{' or '}' or '\n' or '\r';
    }

    private void AppendSegment(string value, int start, int end)
    {
        if (end > start)
        {
            _body.Append(value, start, end - start);
        }
    }

    private void AppendEscapedChar(char ch)
    {
        if (ch > 0x7f)
        {
            _body.Append("\\u");
            _body.Append((int)ch);
            _body.Append('?');
        }
        else
        {
            switch (ch)
            {
                case '\\' or '{' or '}':
                    _body.Append('\\');
                    _body.Append(ch);
                    break;
                case '\n':
                    _body.Append("\\par\r\n");
                    break;
            }
        }
    }

    private string BuildDocument()
    {
        _documentBuilder.Clear();
        _documentBuilder.Append(@"{\rtf1\ansi\deff0");
        _documentBuilder.Append("{\\colortbl ;");
        foreach (var key in _colorTable.Keys)
        {
            _documentBuilder.Append("\\red");
            _documentBuilder.Append(key.R);
            _documentBuilder.Append("\\green");
            _documentBuilder.Append(key.G);
            _documentBuilder.Append("\\blue");
            _documentBuilder.Append(key.B);
            _documentBuilder.Append(';');
        }

        _documentBuilder.Append('}');
        _documentBuilder.Append(_body.AsSpan());
        _documentBuilder.Append('}');

        return _documentBuilder.ToString();
    }

    public void Dispose()
    {
        _body.Dispose();
        _documentBuilder.Dispose();
        _colorTable.Clear();
    }

    public void Clear()
    {
        _body.Clear();
        _documentBuilder.Clear();
        TextLength = 0;
    }
}