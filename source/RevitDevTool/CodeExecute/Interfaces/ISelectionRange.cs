namespace RevitDevTool.CodeExecute.Interfaces;

/// <summary>
/// Interface for text selection range used for highlighting search matches
/// </summary>
public interface ISelectionRange
{
    /// <summary>
    /// Start position of the selection (0-based index)
    /// </summary>
    int Start { get; }

    /// <summary>
    /// End position of the selection (0-based index)
    /// </summary>
    int End { get; }

    /// <summary>
    /// Background color for highlighted text
    /// </summary>
    System.Windows.Media.Color SelectionBackground { get; }

    /// <summary>
    /// Background color for normal (non-highlighted) text
    /// </summary>
    System.Windows.Media.Color NormalBackground { get; }

    /// <summary>
    /// Whether to use dark skin highlighting colors
    /// </summary>
    bool DarkSkin { get; }
}