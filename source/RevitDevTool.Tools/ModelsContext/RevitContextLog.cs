using Microsoft.Extensions.Logging;

namespace RevitDevTool.Tools.ModelsContext;

internal static partial class RevitContextLog
{
    [LoggerMessage(EventId = 101, Level = LogLevel.Trace,
        Message = "Rule unresolved {Document}: {Parameter} has no ids here, the rule matches nothing")]
    public static partial void RuleUnresolved(this ILogger logger, string document, string parameter);

    [LoggerMessage(EventId = 102, Level = LogLevel.Trace,
        Message = "Filter compiled {Document}: {Rules} rules, {Unresolved} unresolved, {Candidates} candidates, exact pass {ExactPass}, compile {ElapsedMs} ms, candidates {CandidatesMs} ms ({CandidateSource})")]
    public static partial void FilterCompiled(this ILogger logger, string document, int rules, int unresolved,
        int candidates, bool exactPass, double elapsedMs, double candidatesMs, string candidateSource);

    [LoggerMessage(EventId = 103, Level = LogLevel.Trace,
        Message = "Match {Document}: {Candidates} candidates, {Native} native, {Matched} matched")]
    public static partial void Matched(this ILogger logger, string document, int candidates, int native,
        int matched);

    [LoggerMessage(EventId = 104, Level = LogLevel.Trace,
        Message = "Document changed {Document}: {Added} added, {Modified} modified, {Deleted} deleted, bulk {Bulk}, handled in {ElapsedMs} ms")]
    public static partial void DocumentChangeHandled(this ILogger logger, string document, int added, int modified,
        int deleted, bool bulk, double elapsedMs);
}
