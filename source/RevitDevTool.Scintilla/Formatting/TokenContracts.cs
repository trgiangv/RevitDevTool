using Microsoft.Extensions.Logging;
using RevitDevTool.Scintilla.Core;
namespace RevitDevTool.Scintilla.Formatting;

public readonly struct LogRenderContext
{
    public LogRenderContext(
        DateTime timestampUtc,
        LogLevel level,
        string? source,
        string? message,
        ReadOnlyMemory<byte> messageBytes,
        string? exceptionText,
        IReadOnlyDictionary<string, object?> properties)
    {
        TimestampUtc = timestampUtc;
        Level = level;
        Source = source;
        Message = message;
        MessageBytes = messageBytes;
        ExceptionText = exceptionText;
        Properties = properties;
    }

    public DateTime TimestampUtc { get; }
    public LogLevel Level { get; }
    public string? Source { get; }
    public string? Message { get; }
    public ReadOnlyMemory<byte> MessageBytes { get; }
    public string? ExceptionText { get; }
    public IReadOnlyDictionary<string, object?> Properties { get; }
}

public readonly struct TokenCandidateContext
{
    /// <summary>
    /// Creates a candidate context for token resolution callbacks.
    /// </summary>
    /// <param name="utf8CandidateBytes">
    /// Pre-computed UTF-8 bytes of <paramref name="candidateText"/>.
    /// Passing this avoids re-encoding inside <see cref="ILogEnrichmentCallbacks"/> implementations
    /// that want to forward the token to byte-level APIs (e.g., Scintilla, ZLogger classifiers).
    /// May be <see cref="ReadOnlyMemory{T}.Empty"/> when not available.
    /// </param>
    public TokenCandidateContext(
        LogRenderContext renderContext,
        string candidateText,
        int utf16Start,
        int utf16Length,
        ReadOnlyMemory<byte> utf8CandidateBytes = default)
    {
        RenderContext = renderContext;
        CandidateText = candidateText;
        Utf16Start = utf16Start;
        Utf16Length = utf16Length;
        Utf8CandidateBytes = utf8CandidateBytes;
    }

    public LogRenderContext RenderContext { get; }
    public string CandidateText { get; }
    public int Utf16Start { get; }
    public int Utf16Length { get; }

    /// <summary>
    /// UTF-8 encoding of <see cref="CandidateText"/>, pre-computed by the rendering pipeline.
    /// Empty when not available — implementations should fall back to encoding <see cref="CandidateText"/>.
    /// </summary>
    public ReadOnlyMemory<byte> Utf8CandidateBytes { get; }
}

public readonly struct TokenResolution
{
    public TokenResolution(int utf16Start, int utf16Length, ILogTokenPayload payload, bool isLink, LogSemanticStyle semanticStyle)
    {
        Utf16Start = utf16Start;
        Utf16Length = utf16Length;
        Payload = payload;
        IsLink = isLink;
        SemanticStyle = semanticStyle;
    }

    public int Utf16Start { get; }
    public int Utf16Length { get; }
    public ILogTokenPayload Payload { get; }
    public bool IsLink { get; }
    public LogSemanticStyle SemanticStyle { get; }
}

public readonly struct TokenResolvedContext
{
    public TokenResolvedContext(LogRenderContext renderContext, string tokenText, TokenResolution resolution)
    {
        RenderContext = renderContext;
        TokenText = tokenText ?? string.Empty;
        Resolution = resolution;
    }

    public LogRenderContext RenderContext { get; }
    public string TokenText { get; }
    public TokenResolution Resolution { get; }
}

public readonly struct TokenClickContext
{
    public TokenClickContext(int documentStart, int length, ILogTokenPayload payload)
    {
        DocumentStart = documentStart;
        Length = length;
        Payload = payload;
    }

    public int DocumentStart { get; }
    public int Length { get; }
    public ILogTokenPayload Payload { get; }
}
