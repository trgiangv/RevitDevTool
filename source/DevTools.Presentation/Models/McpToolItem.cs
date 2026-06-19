using DevTools.McpParser.Models;
using DevTools.UI.Behaviors;
namespace DevTools.Presentation.Models;

public partial class McpToolItem : ObservableObject
{
    [ObservableProperty]
    public partial string ToolId { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DisplayName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SourceAddress { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string GroupName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Description { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ToolTipText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ExecutionMode SourceKind { get; set; } = ExecutionMode.Python;

    [ObservableProperty]
    public partial int CallCount { get; set; }

    [ObservableProperty]
    public partial string InputSchemaJson { get; set; } = "{}";

    [ObservableProperty]
    public partial HighlightRange? NameHighlight { get; set; }

    [ObservableProperty]
    public partial HighlightRange? GroupNameHighlight { get; set; }
}
