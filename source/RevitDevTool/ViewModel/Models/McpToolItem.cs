using RevitDevTool.Execution.Models;
using DevTools.McpParser.Models;
namespace RevitDevTool.ViewModel.Models;

public partial class McpToolItem : ObservableObject
{
    [ObservableProperty] private string _toolId = string.Empty;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private string _sourceAddress = string.Empty;
    [ObservableProperty] private string _groupName = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string _toolTipText = string.Empty;
    [ObservableProperty] private ExecutionMode _sourceKind = ExecutionMode.Python;
    [ObservableProperty] private int _callCount;
    [ObservableProperty] private string _inputSchemaJson = "{}";
    [ObservableProperty] private HighlightRange? _nameHighlight;
    [ObservableProperty] private HighlightRange? _groupNameHighlight;
}