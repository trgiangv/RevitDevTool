using System.ComponentModel;
using DevTools.Execution.Abstractions;
using DevTools.Mcp.Catalog;
using DevTools.Mcp.Core.Utils;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RevitDevTool.Core;

namespace DevTools.Agents.Revit.Tools;

/// <summary>
/// Navigates Revit undo/redo history.
/// Synchronous — operates on main thread via IHostContextExecutor.
/// Returns exact stack state after completion.
/// </summary>
public sealed class NavigateHistoryTool : IBuiltInMcpTool
{
    private readonly IHostContextExecutor _hostContext;

    public NavigateHistoryTool(IHostContextExecutor hostContext)
    {
        _hostContext = hostContext;
        ServerTool = McpServerTool.Create(
            NavigateAsync,
            new McpServerToolCreateOptions
            {
                Name = "navigate_history",
                Title = "Navigate History",
                Description =
                    "Navigate undo/redo history. " +
                    "direction='back' undoes transactions, 'forward' redoes them. " +
                    "Returns the exact history stack state after the operation.",
                Destructive = true,
                OpenWorld = false
            });
    }

    public string Name => "navigate_history";
    public McpServerTool ServerTool { get; }

    [Description("Navigate undo/redo history.")]
    private async Task<CallToolResult> NavigateAsync(
        [Description("Navigation direction: back | forward.")] string direction,
        [Description("Number of steps to navigate (default=1).")] int steps = 1,
        CancellationToken cancellationToken = default)
    {
        if (steps < 1) steps = 1;
        var dir = string.Equals(direction, "forward", StringComparison.OrdinalIgnoreCase)
            ? "forward"
            : "back";

        var resultText = await _hostContext.ExecuteAsync(() =>
            dir == "forward" ? GoForward(steps) : GoBack(steps), cancellationToken).ConfigureAwait(false);

        return ToolHelpers.Result(resultText);
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
        return ToolHelpers.Serialize(new
        {
            direction = "back",
            navigated = actual,
            operations = names,
            back_remaining = stackAfter.Count,
            forward_available = RevitTransactionService.GetCurrentRedoCount()
        });
    }

    private static string GoForward(int steps)
    {
        var redoCount = RevitTransactionService.GetCurrentRedoCount();
        if (redoCount == 0)
            return "Nothing to redo. Forward stack is empty.";

        var actual = Math.Min(steps, redoCount);
        RevitTransactionService.PerformRedo(actual);

        return ToolHelpers.Serialize(new
        {
            direction = "forward",
            navigated = actual,
            back_remaining = RevitTransactionService.GetUndoStack().Count,
            forward_available = RevitTransactionService.GetCurrentRedoCount()
        });
    }
}
