using System.Text;
using RevitDevTool.Scintilla.Core;
using RevitDevTool.Scintilla.Formatting;
using RevitDevTool.Scintilla.Render;

namespace RevitDevTool.Scintilla.Demo;

internal sealed class DemoRevitTokenClassifier : ILogTokenClassifier
{
    // "aurecon" in lowercase UTF-8 bytes — ASCII, so one byte per char.
    // Case-insensitive match: compare against lowercase and uppercase variants.
    private static ReadOnlySpan<byte> Aurecon => "aurecon"u8;

    public bool TryClassify(ReadOnlySpan<byte> utf8Token, out ILogTokenPayload payload)
    {
        payload = default!;

        // Trim leading/trailing ASCII whitespace on the span — no string needed.
        var trimmed = utf8Token.Trim((byte)' ');
        if (trimmed.IsEmpty)
            return false;

        // Fast-path: ASCII case-insensitive compare without decoding to string.
        if (!IsAsciiEqualsIgnoreCase(trimmed, Aurecon))
            return false;

        // Decode only when we need the display string for the payload.
        var displayText = Encoding.UTF8.GetString(utf8Token);

        payload = new DemoTokenPayload(
            "Brand",
            displayText,
            "Aurecon",
            "https://www.aurecongroup.com/",
            StyleToken.TokenClassified,
            LogSemanticStyle.TokenClassified,
            IsLink: false);
        return true;
    }

    // ASCII case-insensitive span compare without heap allocation.
    private static bool IsAsciiEqualsIgnoreCase(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        if (a.Length != b.Length)
            return false;
        for (var i = 0; i < a.Length; i++)
        {
            var ca = a[i] | 0x20; // lowercase ASCII
            var cb = b[i] | 0x20;
            if (ca != cb)
                return false;
        }
        return true;
    }
}

internal sealed record DemoTokenPayload(
    string Kind,
    string DisplayText,
    string NormalizedValue,
    string TargetUri,
    string? StyleKey = StyleToken.TokenLink,
    LogSemanticStyle SemanticStyle = LogSemanticStyle.TokenLink,
    bool IsLink = true)
    : ILogTokenPayload;
