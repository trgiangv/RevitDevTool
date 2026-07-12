using System.Text.Json;
using DevTools.Execution.Abstractions;
using DevTools.Mcp.BuiltIn;
using DevTools.Mcp.Models;
using ModelContextProtocol.Protocol;
using RevitDevTool.Core;

namespace DevTools.Agents.Revit.Tools;

/// <summary>
/// Navigates Revit undo/redo history.
/// Synchronous — operates on main thread via IHostContextExecutor.
/// Returns exact stack state after completion.
/// </summary>
public sealed class NavigateHistoryTool(IHostContextExecutor hostContext) : IBuiltInMcpTool
{
    public string Name => "navigate_history";

    public Tool ProtocolTool { get; } = new()
    {
        Name = "navigate_history",
        Description =
            "Navigate undo/redo history. " +
            "direction='back' undoes transactions, 'forward' redoes them. " +
            "Returns the exact history stack state after the operation.",
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

    public async Task<McpToolExecutionResult> ExecuteAsync(string payloadJson, CancellationToken ct)
    {
        var (direction, steps) = ParseArgs(payloadJson);
        if (steps < 1) steps = 1;

        var resultText = await hostContext.ExecuteAsync(() =>
        {
            if (direction == "forward")
                return GoForward(steps);

            return GoBack(steps);
        }, ct).ConfigureAwait(false);

        return McpToolExecutionResult.Completed(
            new CallToolResult { Content = [new TextContentBlock { Text = resultText }] },
            resultText.Contains("Nothing") || resultText.Contains("Cannot")
                ? "Navigation failed."
                : "Navigation completed.");
    }

    private static string GoBack(int steps)
    {
        var stack = RevitTransactionService.GetUndoStack();
        if (stack.Count == 0)
            return "Nothing to undo. History stack is empty.";

        var actual = Math.Min(steps, stack.Count);
        var names = stack.Take(actual).ToList();
        RevitTransactionService.PerformUndo(actual);

        var stackAfter = RevitTransactionService.GetUndoStack();
        return JsonSerializer.Serialize(new
        {
            direction = "back",
            navigated = actual,
            operations = names,
            back_remaining = stackAfter.Count,
            forward_available = RevitTransactionService.GetCurrentRedoCount()
        }, JsonOpts);
    }

    private static string GoForward(int steps)
    {
        var redoCount = RevitTransactionService.GetCurrentRedoCount();
        if (redoCount == 0)
            return "Nothing to redo. Forward stack is empty.";

        var actual = Math.Min(steps, redoCount);
        RevitTransactionService.PerformRedo(actual);

        return JsonSerializer.Serialize(new
        {
            direction = "forward",
            navigated = actual,
            back_remaining = RevitTransactionService.GetUndoStack().Count,
            forward_available = RevitTransactionService.GetCurrentRedoCount()
        }, JsonOpts);
    }

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
        return (direction, steps);
    }

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };
}
