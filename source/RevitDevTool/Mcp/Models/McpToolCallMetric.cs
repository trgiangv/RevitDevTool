namespace RevitDevTool.Mcp.Models;


public sealed partial class McpToolCallMetric(string toolId, string toolName, int count) : ObservableObject
{
    [ObservableProperty] private string _toolId = toolId;
    [ObservableProperty] private string _toolName = toolName;
    [ObservableProperty] private int _count = count;
}