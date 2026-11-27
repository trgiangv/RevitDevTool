using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Scope;
using DevTools.ModelsContext.Visibility;
using Microsoft.Extensions.Logging;

namespace DevTools.ModelsContext.Diagnostics;

public static partial class ModelContextLog
{
    public static void Swallowed<T>(this ILogger logger, Exception exception, string operation, T subject)
    {
        if (logger.IsEnabled(LogLevel.Trace))
        {
            SwallowedCore(logger, exception, operation, subject?.ToString() ?? string.Empty);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Model Context operation failed: {Reason}")]
    public static partial void Failed(this ILogger logger, Exception exception, string reason);

    [LoggerMessage(EventId = 2, Level = LogLevel.Trace, Message = "{Operation} {Subject}: error ignored")]
    private static partial void SwallowedCore(ILogger logger, Exception exception, string operation, string subject);

    [LoggerMessage(EventId = 3, Level = LogLevel.Trace,
        Message = "{Operation} {Subject}: {Failed} of {Total} failed; first error attached")]
    public static partial void SliceFailed(this ILogger logger, Exception first, string operation, string subject,
        int failed, int total);

    [LoggerMessage(EventId = 5, Level = LogLevel.Trace, Message = "Panel work canceled")]
    public static partial void Canceled(this ILogger logger);

    [LoggerMessage(EventId = 27, Level = LogLevel.Trace, Message = "Context load dropped: a visibility action ran during it")]
    public static partial void LoadDropped(this ILogger logger);

    [LoggerMessage(EventId = 4, Level = LogLevel.Trace, Message = "{Operation} in {ElapsedMs} ms")]
    public static partial void Timed(this ILogger logger, string operation, double elapsedMs);

    [LoggerMessage(EventId = 10, Level = LogLevel.Trace,
        Message = "Snapshot {Source}: {Elements} elements in {ElapsedMs} ms")]
    public static partial void SnapshotBuilt(this ILogger logger, string source, int elements, double elapsedMs);

    [LoggerMessage(EventId = 11, Level = LogLevel.Trace, Message = "Snapshot reload {Source}: {Outcome} in {ElapsedMs} ms")]
    public static partial void SnapshotReloaded(this ILogger logger, string source, ReloadOutcome outcome,
        double elapsedMs);

    [LoggerMessage(EventId = 12, Level = LogLevel.Trace,
        Message = "Value counts {Source} {Parameter}: {Values} values over {Total} elements in {ElapsedMs} ms")]
    public static partial void ValuesCounted(this ILogger logger, string source, string parameter, int values,
        int total, double elapsedMs);

    [LoggerMessage(EventId = 13, Level = LogLevel.Trace,
        Message = "Parameter catalog {Source}: {Parameters} parameters in {ElapsedMs} ms")]
    public static partial void CatalogRead(this ILogger logger, string source, int parameters, double elapsedMs);

    [LoggerMessage(EventId = 14, Level = LogLevel.Trace, Message = "Selection sent: {Elements} elements in {ElapsedMs} ms")]
    public static partial void SelectionSent(this ILogger logger, int elements, double elapsedMs);

    [LoggerMessage(EventId = 15, Level = LogLevel.Trace,
        Message = "Selection received: {Elements} elements applied in {ElapsedMs} ms")]
    public static partial void SelectionReceived(this ILogger logger, int elements, double elapsedMs);

    [LoggerMessage(EventId = 21, Level = LogLevel.Trace,
        Message = "Selection mismatch: host holds {Elements} elements that differ from the panel send")]
    public static partial void SelectionMismatch(this ILogger logger, int elements);

    [LoggerMessage(EventId = 16, Level = LogLevel.Trace,
        Message = "Filter {Filter}: {Matches} matches in {ElapsedMs} ms")]
    public static partial void FilterEvaluated(this ILogger logger, string filter, int matches, double elapsedMs);

    [LoggerMessage(EventId = 22, Level = LogLevel.Trace,
        Message = "Filter {Filter} in {Source}: {Matches} matches in {ElapsedMs} ms")]
    public static partial void FilterDocumentEvaluated(this ILogger logger, string filter, string source, int matches,
        double elapsedMs);

    [LoggerMessage(EventId = 23, Level = LogLevel.Trace,
        Message = "Filter {Filter}: Selection sync paused at {Matches} matches, limit {Limit}")]
    public static partial void FilterSelectionSyncPaused(this ILogger logger, string filter, int matches, int limit);

    [LoggerMessage(EventId = 24, Level = LogLevel.Trace,
        Message = "Parameters merged: {Catalogs} catalogs, {Parameters} parameters, {Scopes} scopes in {ElapsedMs} ms")]
    public static partial void ParametersMerged(this ILogger logger, int catalogs, int parameters, int scopes,
        double elapsedMs);

    [LoggerMessage(EventId = 25, Level = LogLevel.Trace,
        Message = "Deltas applied: {Batches} batches, {Elements} elements in {ElapsedMs} ms")]
    public static partial void DeltasApplied(this ILogger logger, int batches, int elements, double elapsedMs);

    [LoggerMessage(EventId = 17, Level = LogLevel.Trace,
        Message = "Visibility {Action}: {Steps} steps, {Elements} elements in {ElapsedMs} ms")]
    public static partial void VisibilityApplied(this ILogger logger, VisibilityAction action, int steps, int elements,
        double elapsedMs);

    [LoggerMessage(EventId = 18, Level = LogLevel.Trace,
        Message = "Context load {Scope}: {Documents} documents in {ElapsedMs} ms")]
    public static partial void ContextLoaded(this ILogger logger, ScopeKind scope, int documents, double elapsedMs);

    [LoggerMessage(EventId = 19, Level = LogLevel.Trace,
        Message = "Explorer publish: {Rows} rows, rebuilt {Rebuilt} in {ElapsedMs} ms")]
    public static partial void RowsPublished(this ILogger logger, int rows, bool rebuilt, double elapsedMs);

    [LoggerMessage(EventId = 20, Level = LogLevel.Trace,
        Message = "Explorer expand {Row}: {Rows} rows in {ElapsedMs} ms")]
    public static partial void RowExpanded(this ILogger logger, string row, int rows, double elapsedMs);

    [LoggerMessage(EventId = 26, Level = LogLevel.Trace,
        Message = "Changes {Source}: no history since the last read, the snapshot rebuilds")]
    public static partial void ChangesUnavailable(this ILogger logger, string source);
}
