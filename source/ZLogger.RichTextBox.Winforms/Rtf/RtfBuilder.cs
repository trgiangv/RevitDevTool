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
    
    // Cache for color table header - only rebuilt when colors change
    private string? _cachedColorTableHeader;
    private int _lastColorCount;
    
    // Pre-allocated escape sequences for performance
    private const string EscapeBackslash = @"\\";
    private const string EscapeOpenBrace = @"\{";
    private const string EscapeCloseBrace = @"\}";
    private const string EscapeNewline = "\\par\r\n";
    
    public RtfBuilder(Theme theme)
    {
        // Use notNested: false to allow multiple builders on same thread
        // ZString's notNested: true uses thread-local pool and only allows ONE at a time
        _body = ZString.CreateStringBuilder(notNested: false);
        _documentBuilder = ZString.CreateStringBuilder(notNested: false);

        SelectionColor = theme.DefaultStyle.Foreground;
        SelectionBackColor = theme.DefaultStyle.Background;
        _currentFgIndex = RegisterColor(SelectionColor);
        _currentBgIndex = RegisterColor(SelectionBackColor);

        foreach (var color in theme.Colors)
        {
            RegisterColor(color);
        }
        
        // Pre-build color table header
        RebuildColorTableHeader();
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
    
    public void AppendText(ReadOnlySpan<char> text)
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
        _cachedColorTableHeader = null; // Invalidate cache
        return idx;
    }
    
    private void RebuildColorTableHeader()
    {
        if (_lastColorCount == _colorTable.Count && _cachedColorTableHeader != null)
        {
            return;
        }
        
        using var sb = ZString.CreateStringBuilder();
        sb.Append(@"{\rtf1\ansi\deff0");
        sb.Append("{\\colortbl ;");
        foreach (var key in _colorTable.Keys)
        {
            sb.Append("\\red");
            sb.Append(key.R);
            sb.Append("\\green");
            sb.Append(key.G);
            sb.Append("\\blue");
            sb.Append(key.B);
            sb.Append(';');
        }
        sb.Append('}');
        
        _cachedColorTableHeader = sb.ToString();
        _lastColorCount = _colorTable.Count;
    }

    private void EscapeAndAppend(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }
        
        EscapeAndAppend(value.AsSpan());
    }
    
    private void EscapeAndAppend(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty)
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

            if (i > segmentStart)
            {
                _body.Append(value.Slice(segmentStart, i - segmentStart));
            }
            AppendEscapedChar(ch);
            segmentStart = i + 1;
        }

        if (value.Length > segmentStart)
        {
            _body.Append(value.Slice(segmentStart));
        }
    }

    private static bool NeedsEscaping(char ch)
    {
        // Fast path for common ASCII characters
        if (ch is >= ' ' and <= '~' and not ('\\' or '{' or '}'))
        {
            return false;
        }
        
        return ch > 0x7f || ch is '\\' or '{' or '}' or '\n' or '\r';
    }

    private void AppendEscapedChar(char ch)
    {
        switch (ch)
        {
            case '\\':
                _body.Append(EscapeBackslash);
                break;
            case '{':
                _body.Append(EscapeOpenBrace);
                break;
            case '}':
                _body.Append(EscapeCloseBrace);
                break;
            case '\n':
                _body.Append(EscapeNewline);
                break;
            case '\r':
                // Skip carriage returns (handled with \n)
                break;
            default:
                // Unicode escape
                _body.Append("\\u");
                _body.Append((int)ch);
                _body.Append('?');
                break;
        }
    }

    private string BuildDocument()
    {
        // Ensure color table header is up to date
        RebuildColorTableHeader();
        
        _documentBuilder.Clear();
        _documentBuilder.Append(_cachedColorTableHeader!);
        _documentBuilder.Append(_body.AsSpan());
        _documentBuilder.Append('}');

        var result = _documentBuilder.ToString();
        
        // Cap builder capacity to avoid memory bloat
        // If buffer grew too large, trim it back
        const int maxRetainedCapacity = 256 * 1024; // 256KB
        if (_documentBuilder.Length > maxRetainedCapacity)
        {
            _documentBuilder.Dispose();
            _documentBuilder = Cysharp.Text.ZString.CreateStringBuilder(notNested: false);
        }
        if (_body.Length > maxRetainedCapacity)
        {
            _body.Dispose();
            _body = Cysharp.Text.ZString.CreateStringBuilder(notNested: false);
        }
        
        return result;
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
        _currentFgIndex = -1; // Force color reset on next append
        _currentBgIndex = -1;
    }
}