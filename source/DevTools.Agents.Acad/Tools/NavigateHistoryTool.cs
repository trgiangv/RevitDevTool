using System.Text.Json;
using DevTools.Mcp.BuiltIn;
using DevTools.Mcp.Models;
using ModelContextProtocol.Protocol;

namespace DevTools.Agents.Acad.Tools;

/// <summary>
/// Navigates AutoCAD undo/redo history.
/// Does NOT use IHostContextExecutor — Internal.Utils operates at application level
/// and SendMenuStringToExecute conflicts with document lock.
/// </summary>
public sealed class NavigateHistoryTool(AcadHistoryNavigator navigator) : IBuiltInMcpTool
{
    public string Name => "navigate_history";

    public Tool ProtocolTool { get; } = new()
    {
        Name = "navigate_history",
        Description =
            "Navigate undo/redo history. " +
            "direction='back' undoes operations, 'forward' redoes them. " +
            "Returns the history stack state after queuing the navigation. " +
            "Note: executes asynchronously — stack counts are estimates.",
        InputSchema = DevTools.Mcp.Schema.McpSchemaBuilder.Object(
        [
            DevTools.Mcp.Schema.McpSchemaBuilder.Enum("direction",
                "Navigation direction.", ["back", "forward"]),
            DevTools.Mcp.Schema.McpSchemaBuilder.Integer("steps",
                "Number of steps to navigate (default=1).")
        ],
        required: ["direction"]),
        Annotations = new ToolAnnotations
        {
            Title = "Navigate History",
            DestructiveHint = true
        }
    };

    public Task<McpToolExecutionResult> ExecuteAsync(string payloadJson, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var (direction, steps) = ParseArgs(payloadJson);

        if (direction == "forward")
            return Task.FromResult(GoForward(steps));

        return Task.FromResult(GoBack(steps));
    }

    private McpToolExecutionResult GoBack(int steps)
    {
        var stack = navigator.GetBackStack();
        if (stack.Count == 0)
            return Result("Nothing to undo. History stack is empty.");

        var actual = Math.Min(steps, stack.Count);
        var names = stack.Take(actual).ToList();

        if (!navigator.GoBack(actual))
            return Result("Cannot navigate back: host is not in quiescent state.");

        return Result(JsonSerializer.Serialize(new
        {
            direction = "back",
            navigated = actual,
            operations = names,
            back_remaining = Math.Max(0, stack.Count - actual),
            forward_available = navigator.GetForwardStack().Count + actual
        }, JsonOpts));
    }

    private McpToolExecutionResult GoForward(int steps)
    {
        var stack = navigator.GetForwardStack();
        if (stack.Count == 0)
            return Result("Nothing to redo. Forward stack is empty.");

        var actual = Math.Min(steps, stack.Count);
        var names = stack.Take(actual).ToList();

        if (!navigator.GoForward(actual))
            return Result("Cannot navigate forward: host is not in quiescent state.");

        return Result(JsonSerializer.Serialize(new
        {
            direction = "forward",
            navigated = actual,
            operations = names,
            back_remaining = navigator.GetBackStack().Count + actual,
            forward_available = Math.Max(0, stack.Count - actual)
        }, JsonOpts));
    }

    private static McpToolExecutionResult Result(string text) =>
        McpToolExecutionResult.Completed(
            new CallToolResult { Content = [new TextContentBlock { Text = text }] },
            text.Contains("Nothing") || text.Contains("Cannot") ? "Navigation failed." : "Navigation queued.");

    private static (string direction, int steps) ParseArgs(string json)
    {
        var direction = "back";
        var steps = 1;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("direction", out var d))
                direction = d.GetString() ?? "back";
            if (doc.RootElement.TryGetProperty("steps", out var s) && s.TryGetInt32(out var n))
                steps = n;
        }
        catch { /* defaults */ }
        if (steps < 1) steps = 1;
        return (direction, steps);
    }

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };
}
