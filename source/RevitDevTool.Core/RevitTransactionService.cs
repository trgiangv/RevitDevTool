using UIFrameworkServices;
namespace RevitDevTool.Core;

/// <summary>
/// Manages transaction state for rollback functionality.
/// </summary>
/// <example>
/// <code>
/// RevitTransactionService.StartChanges();
/// // ... do work ...
/// RevitTransactionService.RevertChanges();
///
/// // Scoped usage (auto-rollback on dispose):
/// using var scope = RevitTransactionService.CreateScope();
/// // ... do work ...
/// scope.Commit(); // or let it auto-rollback
/// </code>
/// </example>
[PublicAPI]
public static class RevitTransactionService
{
    private static int _count;
    private static readonly Dictionary<string, int> CountDic = new();
    private static readonly Lock Lock = new();

    /// <summary>
    /// Start tracking changes from the current undo stack position (synchronous).
    /// Call this at the beginning of a test or operation to mark the starting point.
    /// </summary>
    /// <returns>Current undo stack count</returns>
    /// <remarks>Must be called from Revit main thread context.</remarks>
    public static int StartChanges()
    {
        lock (Lock)
        {
            _count = GetCurrentUndoCount();
            return _count;
        }
    }

    /// <summary>
    /// Start tracking changes with a specific key (synchronous).
    /// Useful for nested or parallel scenarios.
    /// </summary>
    /// <param name="key">Unique key to identify this tracking session</param>
    /// <returns>Current undo stack count</returns>
    /// <remarks>Must be called from Revit main thread context.</remarks>
    public static int StartChangesWithKey(string key)
    {
        lock (Lock)
        {
            var count = GetCurrentUndoCount();
            CountDic[key] = count;
            return count;
        }
    }

    /// <summary>
    /// Apply and commit all changes, updating the baseline undo count (synchronous).
    /// </summary>
    /// <returns>New baseline undo count</returns>
    /// <remarks>Must be called from Revit main thread context.</remarks>
    public static int ApplyChanges()
    {
        lock (Lock)
        {
            _count = GetCurrentUndoCount();
            return _count;
        }
    }

    /// <summary>
    /// Apply and commit changes for a specific key (synchronous).
    /// </summary>
    /// <param name="key">Key to update</param>
    /// <returns>New baseline undo count for the key</returns>
    /// <remarks>Must be called from Revit main thread context.</remarks>
    public static int ApplyChangesWithKey(string key)
    {
        lock (Lock)
        {
            var count = GetCurrentUndoCount();
            CountDic[key] = count;
            return count;
        }
    }

    /// <summary>
    /// Revert all changes made since StartChanges() was called (synchronous).
    /// This uses Revit's undo stack to rollback transactions.
    /// </summary>
    /// <returns>Number of undo operations performed</returns>
    /// <remarks>Must be called from Revit main thread context.</remarks>
    public static int RevertChanges()
    {
        lock (Lock)
        {
            var currentCount = GetCurrentUndoCount();
            var undoItems = currentCount - _count;

            if (undoItems > 0)
            {
                PerformUndo(undoItems);
            }

            _count = GetCurrentUndoCount();
            return undoItems > 0 ? undoItems : 0;
        }
    }

    /// <summary>
    /// Revert changes for a specific key (synchronous).
    /// </summary>
    /// <param name="key">Key to revert</param>
    /// <returns>Number of undo operations performed</returns>
    /// <remarks>Must be called from Revit main thread context.</remarks>
    public static int RevertChangesWithKey(string key)
    {
        lock (Lock)
        {
            if (!CountDic.TryGetValue(key, out var count)) return 0;

            var currentCount = GetCurrentUndoCount();
            var undoItems = currentCount - count;

            if (undoItems > 0)
            {
                PerformUndo(undoItems);
            }

            CountDic[key] = GetCurrentUndoCount();
            return undoItems > 0 ? undoItems : 0;
        }
    }

    /// <summary>
    /// Clear tracking for a specific key.
    /// </summary>
    /// <param name="key">Key to remove</param>
    /// <returns>True if key was removed</returns>
    public static bool ClearKey(string key)
    {
        lock (Lock)
        {
            return CountDic.Remove(key);
        }
    }

    /// <summary>
    /// Clear all tracking keys.
    /// </summary>
    public static void ClearAllKeys()
    {
        lock (Lock)
        {
            CountDic.Clear();
        }
    }

    #region Utility Methods

    /// <summary>
    /// Remove a specific transaction by name from the undo stack.
    /// </summary>
    /// <param name="transactionName">Name of the transaction to remove</param>
    /// <returns>True if transaction was found and undone</returns>
    /// <remarks>Must be called from Revit main thread context.</remarks>
    public static bool RemoveTransaction(string transactionName)
    {
        var currentUndoStack = QuickAccessToolBarService.collectUndoRedoItems(true);
        if (currentUndoStack.All(x => x != transactionName)) return false;

        PerformUndo(1);
        return true;
    }

    /// <summary>
    /// Get the current undo stack count.
    /// </summary>
    /// <remarks>Must be called from Revit main thread context.</remarks>
    public static int GetCurrentUndoCount()
    {
        return QuickAccessToolBarService.collectUndoRedoItems(true).Count;
    }

    /// <summary>
    /// Get the current redo stack count.
    /// </summary>
    /// <remarks>Must be called from Revit main thread context.</remarks>
    public static int GetCurrentRedoCount()
    {
        return QuickAccessToolBarService.collectUndoRedoItems(false).Count;
    }

    /// <summary>
    /// Get the undo stack items as strings.
    /// </summary>
    /// <remarks>Must be called from Revit main thread context.</remarks>
    public static IReadOnlyList<string> GetUndoStack()
    {
        return QuickAccessToolBarService.collectUndoRedoItems(true);
    }

    /// <summary>
    /// Get the redo stack items as strings.
    /// </summary>
    /// <remarks>Must be called from Revit main thread context.</remarks>
    public static IReadOnlyList<string> GetRedoStack()
    {
        return QuickAccessToolBarService.collectUndoRedoItems(false);
    }

    /// <summary>
    /// Perform multiple undo operations.
    /// </summary>
    /// <param name="count">Number of undo operations</param>
    /// <remarks>Must be called from Revit main thread context.</remarks>
    public static void PerformUndo(int count)
    {
        if (count > 0)
        {
            QuickAccessToolBarService.performMultipleUndoRedoOperations(true, count);
        }
    }

    /// <summary>
    /// Perform multiple redo operations.
    /// </summary>
    /// <param name="count">Number of redo operations</param>
    /// <remarks>Must be called from Revit main thread context.</remarks>
    public static void PerformRedo(int count)
    {
        if (count > 0)
        {
            QuickAccessToolBarService.performMultipleUndoRedoOperations(false, count);
        }
    }

    /// <summary>
    /// Check if there are tracked changes since StartChanges() was called.
    /// </summary>
    /// <remarks>Must be called from Revit main thread context.</remarks>
    public static bool HasChanges()
    {
        return GetCurrentUndoCount() > _count;
    }


    /// <summary>
    /// Check if there are tracked changes for a specific key.
    /// </summary>
    /// <remarks>Must be called from Revit main thread context.</remarks>
    public static bool HasChangesWithKey(string key)
    {
        if (!CountDic.TryGetValue(key, out var count)) return false;
        return GetCurrentUndoCount() > count;
    }

    /// <summary>
    /// Get the number of pending changes since StartChanges().
    /// </summary>
    /// <remarks>Must be called from Revit main thread context.</remarks>
    public static int GetPendingChangesCount()
    {
        var diff = GetCurrentUndoCount() - _count;
        return diff > 0 ? diff : 0;
    }

    /// <summary>
    /// Get the number of pending changes for a specific key.
    /// </summary>
    /// <remarks>Must be called from Revit main thread context.</remarks>
    public static int GetPendingChangesCountWithKey(string key)
    {
        if (!CountDic.TryGetValue(key, out var count)) return 0;
        var diff = GetCurrentUndoCount() - count;
        return diff > 0 ? diff : 0;
    }


    #endregion

    #region Scoped Tracking (Using Pattern)

    /// <summary>
    /// Create a scoped change tracker that automatically reverts on dispose.
    /// </summary>
    /// <returns>A disposable scope that tracks and can rollback changes</returns>
    /// <remarks>Must be called from Revit main thread context.</remarks>
    /// <example>
    /// <code>
    /// using var scope = TransactionService.CreateScope();
    /// // ... make changes ...
    /// scope.Commit(); // Keep changes
    /// // If Commit() not called, changes are rolled back on dispose
    /// </code>
    /// </example>
    public static ChangeScope CreateScope()
    {
        return new ChangeScope();
    }

    /// <summary>
    /// Create a scoped change tracker with a specific key.
    /// </summary>
    /// <param name="key">Unique key for this scope</param>
    /// <returns>A disposable scope that tracks and can rollback changes</returns>
    /// <remarks>Must be called from Revit main thread context.</remarks>
    public static ChangeScope CreateScope(string key)
    {
        return new ChangeScope(key);
    }

    /// <summary>
    /// Disposable scope for automatic change tracking and rollback.
    /// </summary>
    /// <remarks>
    /// <para>On dispose, if <see cref="Commit"/> was not called, changes are automatically rolled back.</para>
    /// </remarks>
    [PublicAPI]
    public sealed class ChangeScope : IDisposable
    {
        private readonly string? _key;
        private bool _disposed;

        internal ChangeScope()
        {
            _key = null;
            StartChanges();
        }

        internal ChangeScope(string key)
        {
            _key = key;
            StartChangesWithKey(key);
        }

        /// <summary>
        /// Commit changes, preventing rollback on dispose (synchronous).
        /// </summary>
        /// <remarks>Must be called from Revit main thread context.</remarks>
        public void Commit()
        {
            IsCommitted = true;
            if (_key is null)
                ApplyChanges();
            else
                ApplyChangesWithKey(_key);
        }

        /// <summary>
        /// Manually rollback changes (synchronous).
        /// </summary>
        /// <returns>Number of undo operations performed</returns>
        /// <remarks>Must be called from Revit main thread context.</remarks>
        public int Rollback()
        {
            if (IsCommitted) return 0;
            return _key is null ? RevertChanges() : RevertChangesWithKey(_key);
        }

        /// <summary>
        /// Check if there are pending changes in this scope (synchronous).
        /// </summary>
        /// <remarks>Must be called from Revit main thread context.</remarks>
        public bool HasPendingChanges => _key is null ? HasChanges() : HasChangesWithKey(_key);

        /// <summary>
        /// Get number of pending changes in this scope (synchronous).
        /// </summary>
        /// <remarks>Must be called from Revit main thread context.</remarks>
        public int PendingChangesCount => _key is null ? GetPendingChangesCount() : GetPendingChangesCountWithKey(_key);

        /// <summary>
        /// Check if changes have been committed.
        /// </summary>
        public bool IsCommitted { get; private set; }

        /// <summary>
        /// Dispose the scope. If not committed, changes are rolled back.
        /// </summary>
        /// <remarks>
        /// <para>Must be called from Revit main thread context for auto-rollback to work.</para>
        /// </remarks>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (!IsCommitted)
            {
                try
                {
                    Rollback();
                }
                catch
                {
                    // Ignore errors during dispose - may be called from non-Revit context
                }
            }

            if (_key is not null)
            {
                ClearKey(_key);
            }
        }
    }

    #endregion
}