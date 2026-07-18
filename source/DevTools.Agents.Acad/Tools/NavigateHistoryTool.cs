using System.Text.Json;
using System.ComponentModel;
using DevTools.Mcp.BuiltIn;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Agents.Acad.Tools;

/// <summary>
/// Navigates AutoCAD undo/redo history.
/// Does NOT use IHostContextExecutor — Internal.Utils operates at application level
/// and SendMenuStringToExecute conflicts with document lock.
/// </summary>
public sealed class NavigateHistoryTool(AcadHistoryNavigator navigator) : IBuiltInMcpTool
{
    public McpServerTool Primitive => McpServerTool.Create(typeof(NavigateHistoryTool).GetMethod(nameof(NavigateHistoryAsync))!, this);

    [McpServerTool(Name = "navigate_history")]
    [Description("Navigate AutoCAD undo/redo history.")]
    public Task<CallToolResult> NavigateHistoryAsync(
        [Description("Navigation direction: back or forward.")] string direction,
        [Description("Number of steps to navigate.")] int steps = 1,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (steps < 1) throw new McpException("steps must be at least 1.");
        if (!string.Equals(direction, "back", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(direction, "forward", StringComparison.OrdinalIgnoreCase))
            throw new McpException("direction must be either 'back' or 'forward'.");

        if (string.Equals(direction, "forward", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(GoForward(steps));

        return Task.FromResult(GoBack(steps));
    }

    private CallToolResult GoBack(int steps)
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

    private CallToolResult GoForward(int steps)
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

    private static CallToolResult Result(string text) =>
        new() { Content = [new TextContentBlock { Text = text }] };

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };
}
