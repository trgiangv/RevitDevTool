using DevTools.Execution.Abstractions;

namespace DevTools.Presentation.Models;

public partial class McpToolItem : ObservableObject
{
    [ObservableProperty]
    public partial string ToolId { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SourceAddress { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string GroupName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ToolTipText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ExecutionMode SourceKind { get; set; } = ExecutionMode.Python;

    [ObservableProperty]
    public partial int CallCount { get; set; }

    [ObservableProperty]
    public partial TextHighlightRange? NameHighlight { get; set; }

    [ObservableProperty]
    public partial TextHighlightRange? GroupNameHighlight { get; set; }
}
