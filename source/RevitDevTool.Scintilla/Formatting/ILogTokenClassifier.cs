namespace RevitDevTool.Scintilla.Formatting;

/// <summary>
/// Classifies a raw UTF-8 token from the log stream.
/// Implementations should operate directly on the <see cref="ReadOnlySpan{T}"/> to avoid any
/// string allocation on the hot path.  Use <see cref="System.Text.Encoding.UTF8"/> only as a
/// last resort for non-ASCII matching.
/// </summary>
/// <remarks>
/// <para>
/// Prefer byte-level comparison with UTF-8 literals (<c>"value"u8</c>) or trie-based matching.
/// For ASCII-only patterns (Revit element IDs, GUIDs, numbers) no decoding is needed at all.
/// </para>
/// <para>
/// Breaking change from the string-based API: callers now pass a UTF-8 byte slice so the
/// classifier can operate in the same encoding plane as Scintilla's document buffer.
/// </para>
/// </remarks>
public interface ILogTokenClassifier
{
    bool TryClassify(ReadOnlySpan<byte> utf8Token, out ILogTokenPayload payload);
}
