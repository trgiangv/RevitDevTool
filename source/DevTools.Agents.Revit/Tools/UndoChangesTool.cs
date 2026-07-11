using System.Text.Json;
using DevTools.Execution.Abstractions;
using DevTools.Mcp.BuiltIn;
using DevTools.Mcp.Models;
using ModelContextProtocol.Protocol;
using RevitDevTool.Core;

namespace DevTools.Agents.Revit.Tools;

/// <summary>
/// Undoes recent changes made via MCP tool calls.
/// Uses Revit's undo stack to rollback transactions.
/// Must execute on host main thread (QuickAccessToolBarService requires it).
/// </summary>
public sealed class UndoChangesTool(IHostContextExecutor hostContext) : IBuiltInMcpTool
{
    public string Name => "undo_changes";

    public Tool ProtocolTool { get; } = new()
    {
        Name = "undo_changes",
        Description =
            "Undo recent Revit transactions. " +
            "count=1 undoes the last transaction; count=N undoes N transactions. " +
            "Returns the undo stack state after the operation.",
        InputSchema = DevTools.Mcp.Schema.McpSchemaBuilder.Object(
        [
            DevTools.Mcp.Schema.McpSchemaBuilder.Integer("count",
                "Number of transactions to undo (default=1).")
        ],
        required: []),
        Annotations = new ToolAnnotations
        {
            Title = "Undo Changes",
            DestructiveHint = true
        }
    };

    public async Task<McpToolExecutionResult> ExecuteAsync(string payloadJson, CancellationToken ct)
    {
        var count = ParseCount(payloadJson);
        if (count < 1) count = 1;

        var resultText = await hostContext.ExecuteAsync(() =>
        {
            var stackBefore = RevitTransactionService.GetUndoStack();
            if (stackBefore.Count == 0)
                return "Nothing to undo. Undo stack is empty.";

            var actual = Math.Min(count, stackBefore.Count);
            var undoneNames = stackBefore.Take(actual).ToList();
            RevitTransactionService.PerformUndo(actual);

            var stackAfter = RevitTransactionService.GetUndoStack();
            var summary = new
            {
                undone = actual,
                transactions = undoneNames,
                remaining_undo_stack = stackAfter.Count,
                redo_available = RevitTransactionService.GetCurrentRedoCount()
            };
            return JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true });
        }, ct).ConfigureAwait(false);

        return McpToolExecutionResult.Completed(
            new CallToolResult
            {
                Content = [new TextContentBlock { Text = resultText }]
            },
            resultText.Contains("Nothing") ? "No changes to undo." : "Undo completed.");
    }

    private static int ParseCount(string payloadJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            if (doc.RootElement.TryGetProperty("count", out var countElement) &&
                countElement.TryGetInt32(out var c))
                return c;
        }
        catch { /* default */ }
        return 1;
    }
}
