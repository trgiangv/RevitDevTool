using System.Buffers;
using System.Text;
namespace RevitDevTool.Scintilla.Formatting;

internal sealed class TokenResolutionEngine
{
    private readonly ILogEnrichmentCallbacks? _callbacks;
    private readonly ILogTokenClassifier? _classifier;
    private readonly Action<Exception>? _errorSink;

    public TokenResolutionEngine(
        ILogEnrichmentCallbacks? callbacks,
        ILogTokenClassifier? classifier,
        Action<Exception>? errorSink)
    {
        _callbacks = callbacks;
        _classifier = classifier;
        _errorSink = errorSink;
    }

    public bool HasResolvers => _callbacks is not null || _classifier is not null;

    public bool TryResolveToken(
        LogRenderContext context,
        string tokenText,
        int start,
        int length,
        RenderTokenWriter writer)
    {
        // ── zero-copy byte slice (ASCII fast path) ───────────────────────────────────────────────
        // LogRenderContext.MessageBytes is the UTF-8 buffer that corresponds to LogRenderContext.Message.
        // For ASCII tokens (code-points U+0000..U+007F), char[i] == byte[i], so the char offset
        // 'start' is also the byte offset — we can slice directly without any encoding work.
        // TrySliceAsciiTokenBytes verifies this assumption byte-by-byte in O(n).
        // Non-ASCII or offset-drifted tokens fall back to the ArrayPool encode path below.
        var directBytes = TrySliceAsciiTokenBytes(context, tokenText, start);

        // ── enrichment callbacks path ────────────────────────────────────────────────────────────
        if (_callbacks is not null)
        {
            try
            {
                // Pass directBytes so callback implementations receive pre-computed UTF-8
                // bytes without any allocation — they can forward directly to byte-level APIs.
                var candidate = new TokenCandidateContext(context, tokenText, start, length, directBytes);
                if (_callbacks.TryResolveToken(candidate, out var resolution))
                {
                    writer.Add(tokenText, resolution.SemanticStyle, resolution.Payload, resolution.IsLink);
                    _callbacks.OnTokenResolved(new TokenResolvedContext(context, tokenText, resolution));
                    return true;
                }
            }
            catch (Exception ex)
            {
                _errorSink?.Invoke(ex);
            }
        }

        // ── classifier path ──────────────────────────────────────────────────────────────────────
        if (_classifier is not null)
        {
            if (!directBytes.IsEmpty)
            {
                // Zero-alloc: bytes were sliced directly from the context message buffer.
                // No ArrayPool.Rent, no Encoding.GetBytes — eliminates the bytes→string→bytes round-trip.
                if (_classifier.TryClassify(directBytes.Span, out var payload) && payload is not null)
                {
                    writer.Add(tokenText, payload.SemanticStyle, payload, payload.IsLink, payload.StyleKey);
                    return true;
                }
                // Classifier saw the bytes and declined — no need to try again via encoded path.
                return false;
            }

            // Non-ASCII fallback: encode once with ArrayPool and pass bytes to the classifier.
            var maxBytes = Encoding.UTF8.GetMaxByteCount(tokenText.Length);
            var rented = ArrayPool<byte>.Shared.Rent(maxBytes);
            try
            {
#if NET8_0_OR_GREATER
                var written = Encoding.UTF8.GetBytes(tokenText.AsSpan(), rented.AsSpan(0, maxBytes));
#else
                var written = Encoding.UTF8.GetBytes(tokenText, 0, tokenText.Length, rented, 0);
#endif
                if (_classifier.TryClassify(rented.AsSpan(0, written), out var payload) && payload is not null)
                {
                    writer.Add(tokenText, payload.SemanticStyle, payload, payload.IsLink, payload.StyleKey);
                    return true;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }

        return false;
    }

    // Attempts to return a Memory<byte> slice from context.MessageBytes that maps to the
    // given tokenText at char offset charStart.
    //
    // Succeeds only when all bytes in the slice equal the corresponding chars of tokenText,
    // which is true if and only if every code-point is ASCII (U+0000..U+007F).  Under that
    // condition UTF-8 and UTF-16 encode identically, so the char offset equals the byte offset
    // and no encoding work is required.
    //
    // Returns ReadOnlyMemory.Empty when:
    //   • context.MessageBytes is empty (e.g. plain-text path without bytes context)
    //   • offsets are out of range
    //   • the token contains any non-ASCII character or the positions don't align
    private static ReadOnlyMemory<byte> TrySliceAsciiTokenBytes(
        LogRenderContext context, string tokenText, int charStart)
    {
        if (tokenText.Length == 0 || context.MessageBytes.IsEmpty)
            return default;

        var msgBytes = context.MessageBytes;
        var byteEnd = charStart + tokenText.Length;
        if (byteEnd > msgBytes.Length)
            return default;

        var slice = msgBytes.Slice(charStart, tokenText.Length);
        var span  = slice.Span;

        // Verify byte-by-byte: span[i] == tokenText[i] for every position.
        // Any non-ASCII char (> 127) encodes to multiple bytes in UTF-8, so it would
        // produce a mismatch on the first differing index and we fall through to encoding.
        for (var i = 0; i < tokenText.Length; i++)
        {
            if (span[i] != tokenText[i])
                return default;
        }

        return slice;
    }
}
