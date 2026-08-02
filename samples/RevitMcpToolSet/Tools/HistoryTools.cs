using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RevitMcpToolSet.Utilities;

namespace RevitMcpToolSet.Tools;

[McpServerToolType]
[Description("Undo/redo navigation for agent-driven rollback workflows.")]
public static class HistoryTools
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [McpServerTool(
        Name = "navigate_history",
        Title = "Navigate History",
        Destructive = true,
        OpenWorld = false)]
    [Description(
        "Navigate undo/redo history. direction='back' undoes transactions, 'forward' redoes them. " +
        "Returns the exact history stack state after the operation.")]
    public static CallToolResult Navigate(
        [Description("Navigation direction: back | forward.")] string direction,
        [Description("Number of steps to navigate (default=1).")] int steps = 1)
    {
        if (steps < 1) steps = 1;
        var forward = string.Equals(direction, "forward", StringComparison.OrdinalIgnoreCase);
        var payload = forward ? GoForward(steps) : GoBack(steps);
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = JsonSerializer.Serialize(payload, JsonOptions) }]
        };
    }

    private static object GoBack(int steps)
    {
        var stack = UndoHistoryUtility.GetUndoStack();
        if (stack.Count == 0)
            return new { message = "Nothing to undo. History stack is empty." };

        var actual = Math.Min(steps, stack.Count);
        var names = stack.Take(actual).ToList();
        UndoHistoryUtility.PerformUndo(actual);

        return new
        {
            direction = "back",
            navigated = actual,
            operations = names,
            back_remaining = UndoHistoryUtility.GetUndoStack().Count,
            forward_available = UndoHistoryUtility.GetCurrentRedoCount()
        };
    }

    private static object GoForward(int steps)
    {
        var redoCount = UndoHistoryUtility.GetCurrentRedoCount();
        if (redoCount == 0)
            return new { message = "Nothing to redo. Forward stack is empty." };

        var actual = Math.Min(steps, redoCount);
        UndoHistoryUtility.PerformRedo(actual);

        return new
        {
            direction = "forward",
            navigated = actual,
            back_remaining = UndoHistoryUtility.GetUndoStack().Count,
            forward_available = UndoHistoryUtility.GetCurrentRedoCount()
        };
    }
}
