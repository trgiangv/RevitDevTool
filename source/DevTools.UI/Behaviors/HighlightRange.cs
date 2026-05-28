namespace DevTools.UI.Behaviors;

/// <summary>
/// Default implementation of ISelectionRange for text highlighting
/// </summary>
public class HighlightRange(int start, int end)
{
    public int Start { get; } = start;
    public int End { get; } = end;
    public System.Windows.Media.Color SelectionBackground =>
        DarkSkin
            ? System.Windows.Media.Colors.DarkOrange
            : System.Windows.Media.Color.FromRgb(0xF5, 0x9E, 0x0B); // Amber #F59E0B
    public System.Windows.Media.Color NormalBackground { get; } = System.Windows.Media.Colors.Transparent;
    public bool DarkSkin { get; init; }
}
