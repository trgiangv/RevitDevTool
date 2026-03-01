using RevitDevTool.Scintilla.Core;
namespace RevitDevTool.Scintilla.Formatting;

/// <summary>
/// Unified payload contract for classified log tokens.
/// Replaces the former three-interface hierarchy
/// (ILogTokenPayload / IKeyedLogTokenPayload / IStyledLogTokenPayload).
/// </summary>
public interface ILogTokenPayload
{
    /// <summary>Visual style used to colour the token in the log viewer.</summary>
    LogSemanticStyle SemanticStyle { get; }

    /// <summary>
    /// Optional named style key that overrides <see cref="SemanticStyle"/>.
    /// <see langword="null"/> means fall back to <see cref="SemanticStyle"/>.
    /// </summary>
    string? StyleKey { get; }

    /// <summary>True when the token should be rendered as a clickable link.</summary>
    bool IsLink { get; }
}
