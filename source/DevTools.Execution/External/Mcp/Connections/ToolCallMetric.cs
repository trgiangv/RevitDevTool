namespace DevTools.Execution.External.Mcp.Connections;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed partial class ToolCallMetric(string toolId, string toolName, int count) : ObservableObject
{
    [ObservableProperty]
    public partial string ToolId { get; set; } = toolId;

    [ObservableProperty]
    public partial string ToolName { get; set; } = toolName;

    [ObservableProperty]
    public partial int Count { get; set; } = count;
}