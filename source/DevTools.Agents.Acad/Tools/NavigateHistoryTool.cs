using System.ComponentModel;
using DevTools.Mcp.Catalog;
using DevTools.Mcp.Core.Utils;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Agents.Acad.Tools;

/// <summary>
/// Navigates AutoCAD undo/redo history.
/// Does NOT use IHostContextExecutor — Internal.Utils operates at application level
/// and SendMenuStringToExecute conflicts with document lock.
/// </summary>
public sealed class NavigateHistoryTool : IBuiltInMcpTool
{
    private readonly AcadHistoryNavigator _navigator;

    public NavigateHistoryTool(AcadHistoryNavigator navigator)
    {
        _navigator = navigator;
        ServerTool = McpServerTool.Create(
            Navigate,
            new McpServerToolCreateOptions
            {
                Name = "navigate_history",
                Title = "Navigate History",
                Description =
                    "Navigate undo/redo history. " +
                    "direction='back' undoes operations, 'forward' redoes them. " +
                    "Returns the history stack state after queuing the navigation. " +
                    "Note: executes asynchronously — stack counts are estimates.",
                Destructive = true,
                OpenWorld = false
            });
    }

    public string Name => "navigate_history";
    public McpServerTool ServerTool { get; }

    [Description("Navigate undo/redo history.")]
    private CallToolResult Navigate(
        [Description("Navigation direction: back | forward.")] string direction,
        [Description("Number of steps to navigate (default=1).")] int steps = 1)
    {
        if (steps < 1) steps = 1;
        return string.Equals(direction, "forward", StringComparison.OrdinalIgnoreCase)
            ? GoForward(steps)
            : GoBack(steps);
    }

    private CallToolResult GoBack(int steps)
    {
        var stack = _navigator.GetBackStack();
        if (stack.Count == 0)
            return ToolHelpers.Result("Nothing to undo. History stack is empty.");

        var actual = Math.Min(steps, stack.Count);
        var names = stack.Take(actual).ToList();

        if (!_navigator.GoBack(actual))
            return ToolHelpers.Result("Cannot navigate back: host is not in quiescent state.");

        return ToolHelpers.Result(new
        {
            direction = "back",
            navigated = actual,
            operations = names,
            back_remaining = Math.Max(0, stack.Count - actual),
            forward_available = _navigator.GetForwardStack().Count + actual
        });
    }

    private CallToolResult GoForward(int steps)
    {
        var stack = _navigator.GetForwardStack();
        if (stack.Count == 0)
            return ToolHelpers.Result("Nothing to redo. Forward stack is empty.");

        var actual = Math.Min(steps, stack.Count);
        var names = stack.Take(actual).ToList();

        if (!_navigator.GoForward(actual))
            return ToolHelpers.Result("Cannot navigate forward: host is not in quiescent state.");

        return ToolHelpers.Result(new
        {
            direction = "forward",
            navigated = actual,
            operations = names,
            back_remaining = _navigator.GetBackStack().Count + actual,
            forward_available = Math.Max(0, stack.Count - actual)
        });
    }
}
