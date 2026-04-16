using RevitDevTool.Execution.Interfaces;
namespace RevitDevTool.Execution.Models;

/// <summary>
/// Default implementation of ISelectionRange for text highlighting
/// </summary>
public class HighlightRange(int start, int end) : ISelectionRange
{
    public int Start { get; } = start;
    public int End { get; } = end;
    public System.Windows.Media.Color SelectionBackground => DarkSkin ? System.Windows.Media.Colors.DarkOrange : System.Windows.Media.Colors.Yellow;
    public System.Windows.Media.Color NormalBackground { get; } = System.Windows.Media.Colors.Transparent;
    public bool DarkSkin { get; init; }
}