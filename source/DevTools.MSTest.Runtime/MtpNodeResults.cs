using DevTools.Testing.Abstractions.Contracts;
using Microsoft.Testing.Platform.Extensions.Messages;

namespace DevTools.Testing.Mtp;

#pragma warning disable CS0618
#pragma warning disable MTP0001

/// <summary>
/// MTP <see cref="TestNode"/> to <see cref="TestCaseResult"/> core shared by the
/// MSTest and TUnit runtimes (owned by DevTools.MSTest.Runtime, linked into
/// DevTools.TUnit.Runtime). Framework-specific output capture and full names
/// stay in each runtime.
/// </summary>
internal static class MtpNodeResults
{
    public static bool HasTerminalState(TestNode node)
    {
        var properties = node.Properties;
        return properties.SingleOrDefault<SkippedTestNodeStateProperty>() is not null
            || properties.SingleOrDefault<FailedTestNodeStateProperty>() is not null
            || properties.SingleOrDefault<ErrorTestNodeStateProperty>() is not null
            || properties.SingleOrDefault<TimeoutTestNodeStateProperty>() is not null
            || properties.SingleOrDefault<CancelledTestNodeStateProperty>() is not null
            || properties.SingleOrDefault<PassedTestNodeStateProperty>() is not null;
    }

    public static string? FrameworkOutput(TestNode node)
    {
        var stdout = node.Properties.SingleOrDefault<StandardOutputProperty>()?.StandardOutput;
        var stderr = node.Properties.SingleOrDefault<StandardErrorProperty>()?.StandardError;
        if (string.IsNullOrWhiteSpace(stdout))
            return string.IsNullOrWhiteSpace(stderr) ? null : stderr;
        return string.IsNullOrWhiteSpace(stderr) ? stdout : stdout + Environment.NewLine + stderr;
    }

    /// <summary>
    /// Maps a node in a terminal state. A node still in progress when the run was
    /// cancelled counts as cancelled; any other non-terminal node maps to null.
    /// <c>FullName</c> is the MTP uid; callers replace it when they know better.
    /// </summary>
    public static TestCaseResult? Map(TestNode node, bool runCancelled)
    {
        var properties = node.Properties;
        var skipped = properties.SingleOrDefault<SkippedTestNodeStateProperty>();
        var failed = properties.SingleOrDefault<FailedTestNodeStateProperty>();
        var error = properties.SingleOrDefault<ErrorTestNodeStateProperty>();
        var timeout = properties.SingleOrDefault<TimeoutTestNodeStateProperty>();
        var cancelled = properties.SingleOrDefault<CancelledTestNodeStateProperty>();
        var terminal = HasTerminalState(node);
        var abandoned = !terminal
            && runCancelled
            && properties.SingleOrDefault<InProgressTestNodeStateProperty>() is not null;
        if (!terminal && !abandoned)
            return null;

        var outcome = cancelled is not null || abandoned ? TestOutcomes.Cancelled
            : skipped is not null ? TestOutcomes.Skipped
            : error is not null ? TestOutcomes.Error
            : failed is not null || timeout is not null ? TestOutcomes.Failed
            : TestOutcomes.Passed;
        var exception = failed?.Exception ?? error?.Exception ?? timeout?.Exception ?? cancelled?.Exception;
        var message = skipped?.Explanation
            ?? failed?.Explanation
            ?? error?.Explanation
            ?? timeout?.Explanation
            ?? cancelled?.Explanation
            ?? exception?.Message;
        var timing = properties.SingleOrDefault<TimingProperty>();
        var location = properties.SingleOrDefault<TestFileLocationProperty>();
        var output = FrameworkOutput(node);
        var testId = node.Uid.Value;
        return new TestCaseResult(
            testId,
            node.DisplayName,
            outcome,
            timing?.GlobalTiming.Duration.TotalMilliseconds ?? 0,
            message,
            exception?.StackTrace,
            string.IsNullOrWhiteSpace(output) ? null : output,
            location is null
                ? null
                : new TestSourceLocation(location.FilePath, location.LineSpan.Start.Line),
            [],
            [],
            FullName: testId,
            SkipReason: skipped?.Explanation);
    }
}