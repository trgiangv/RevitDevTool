namespace RevitDevTool.Core.Execution;

/// <summary>
/// Internal tracking of suppressed events during a guard scope.
/// Only rollback information is surfaced to callers; everything else is for internal logging.
/// </summary>
internal sealed class ExecutionGuardFeedback
{
    private readonly List<string> _rolledBackFailures = [];
    private int _dialogsSuppressed;
    private int _warningsDismissed;
    private int _errorsResolved;

    internal bool HadRollback => _rolledBackFailures.Count > 0;

    internal void RecordDialogSuppressed() => _dialogsSuppressed++;
    internal void RecordWarningDismissed() => _warningsDismissed++;
    internal void RecordErrorResolved() => _errorsResolved++;

    internal void RecordRollback(string failureId)
        => _rolledBackFailures.Add(failureId);

    /// <summary>
    /// Summary of rolled-back failures for AI feedback. Null if no rollback occurred.
    /// </summary>
    internal string? GetRollbackSummary()
    {
        if (_rolledBackFailures.Count == 0) return null;
        return $"Transaction rolled back due to {_rolledBackFailures.Count} unresolvable failure(s): [{string.Join(", ", _rolledBackFailures)}]";
    }

    /// <summary>
    /// Full internal log summary (for file/dev logging, not exposed to AI).
    /// </summary>
    internal string ToLogSummary()
    {
        var parts = new List<string>(4);
        if (_dialogsSuppressed > 0) parts.Add($"{_dialogsSuppressed} dialog(s) suppressed");
        if (_warningsDismissed > 0) parts.Add($"{_warningsDismissed} warning(s) dismissed");
        if (_errorsResolved > 0) parts.Add($"{_errorsResolved} error(s) auto-resolved");
        if (_rolledBackFailures.Count > 0) parts.Add($"{_rolledBackFailures.Count} failure(s) rolled back");
        return parts.Count > 0 ? string.Join(", ", parts) : string.Empty;
    }
}
