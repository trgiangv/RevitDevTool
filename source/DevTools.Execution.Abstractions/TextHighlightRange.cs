namespace DevTools.Execution.Abstractions;

/// <summary>
/// Host-neutral text highlight span (start/end indices). WPF colors live in the UI layer.
/// </summary>
public sealed class TextHighlightRange(int start, int end)
{
    public int Start { get; } = start;
    public int End { get; } = end;
    public bool DarkSkin { get; init; }
}
