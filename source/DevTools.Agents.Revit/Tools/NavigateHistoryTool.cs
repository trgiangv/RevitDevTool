using System.Text.Json;
using System.ComponentModel;
using DevTools.Execution.Abstractions;
using DevTools.Mcp.BuiltIn;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RevitDevTool.Core;

namespace DevTools.Agents.Revit.Tools;

/// <summary>
/// Navigates Revit undo/redo history.
/// Synchronous — operates on main thread via IHostContextExecutor.
/// Returns exact stack state after completion.
/// </summary>
public sealed class NavigateHistoryTool(IHostContextExecutor hostContext) : IBuiltInMcpTool
{
    public McpServerTool Primitive => McpServerTool.Create(typeof(NavigateHistoryTool).GetMethod(nameof(NavigateHistoryAsync))!, this);

    [McpServerTool(Name = "navigate_history")]
    [Description("Navigate Revit undo/redo history.")]
    public async Task<CallToolResult> NavigateHistoryAsync(
        [Description("Navigation direction: back or forward.")] string direction,
        [Description("Number of steps to navigate.")] int steps = 1,
        CancellationToken cancellationToken = default)
    {
        if (steps < 1) throw new McpException("steps must be at least 1.");
        if (!string.Equals(direction, "back", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(direction, "forward", StringComparison.OrdinalIgnoreCase))
            throw new McpException("direction must be either 'back' or 'forward'.");

        var resultText = await hostContext.ExecuteAsync(() =>
        {
            if (string.Equals(direction, "forward", StringComparison.OrdinalIgnoreCase))
                return GoForward(steps);

            return GoBack(steps);
        }, cancellationToken).ConfigureAwait(false);

        return new CallToolResult { Content = [new TextContentBlock { Text = resultText }] };
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

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };
}
