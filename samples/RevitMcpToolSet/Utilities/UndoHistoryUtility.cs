using UIFrameworkServices;

namespace RevitMcpToolSet.Utilities;

/// <summary>Undo/redo stack access for <c>navigate_history</c> (mirrors host <c>RevitTransactionService</c>).</summary>
internal static class UndoHistoryUtility
{
    public static IReadOnlyList<string> GetUndoStack() =>
        QuickAccessToolBarService.collectUndoRedoItems(true);

    public static int GetCurrentRedoCount() =>
        QuickAccessToolBarService.collectUndoRedoItems(false).Count;

    public static void PerformUndo(int count)
    {
        if (count > 0)
            QuickAccessToolBarService.performMultipleUndoRedoOperations(true, count);
    }

    public static void PerformRedo(int count)
    {
        if (count > 0)
            QuickAccessToolBarService.performMultipleUndoRedoOperations(false, count);
    }
}
