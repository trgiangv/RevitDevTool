namespace RevitDevTool.ExternalExecution.Connections;

public sealed partial class ToolCallMetric(string toolId, string toolName, int count) : ObservableObject
{
    [ObservableProperty] private string _toolId = toolId;
    [ObservableProperty] private string _toolName = toolName;
    [ObservableProperty] private int _count = count;
}