using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Runtime;
using Microsoft.Testing.Platform.Extensions.Messages;

namespace DevTools.TUnit.Runtime;

#pragma warning disable CS0618
#pragma warning disable MTP0001

internal static class TUnitEngineResults
{
    public static IReadOnlyList<TestCaseResult> Map(
        IEnumerable<TestNode> nodes,
        IReadOnlyDictionary<string, string?>? capturedByUid = null)
    {
        var results = new List<TestCaseResult>();
        foreach (var node in nodes)
        {
            var mapped = Map(node);
            if (mapped is null)
                continue;

            if (capturedByUid is not null
                && capturedByUid.TryGetValue(node.Uid.Value, out var captured))
            {
                mapped = mapped with { Output = TestRunTraceScope.Merge(mapped.Output, captured) };
            }

            results.Add(mapped);
        }

        return results;
    }

    internal static bool IsTerminal(TestNode node) => Map(node) is not null;

    internal static string? FrameworkOutput(TestNode node)
    {
        var stdout = node.Properties.SingleOrDefault<StandardOutputProperty>()?.StandardOutput;
        var stderr = node.Properties.SingleOrDefault<StandardErrorProperty>()?.StandardError;
        return Combine(stdout, stderr);
    }

    private static TestCaseResult? Map(TestNode node)
    {
        var properties = node.Properties;
        var skipped = properties.SingleOrDefault<SkippedTestNodeStateProperty>();
        var failed = properties.SingleOrDefault<FailedTestNodeStateProperty>();
        var error = properties.SingleOrDefault<ErrorTestNodeStateProperty>();
        var timeout = properties.SingleOrDefault<TimeoutTestNodeStateProperty>();
        var cancelled = properties.SingleOrDefault<CancelledTestNodeStateProperty>();
        var passed = properties.SingleOrDefault<PassedTestNodeStateProperty>();
        if (skipped is null && failed is null && error is null && timeout is null && cancelled is null && passed is null)
            return null;

        var outcome = cancelled is not null ? TestOutcomes.Cancelled
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
        return new TestCaseResult(
            node.Uid.Value,
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
            FullName: node.Uid.Value,
            SkipReason: skipped?.Explanation);
    }

    private static string? Combine(string? stdout, string? stderr)
    {
        if (string.IsNullOrWhiteSpace(stdout))
            return string.IsNullOrWhiteSpace(stderr) ? null : stderr;
        return string.IsNullOrWhiteSpace(stderr) ? stdout : $"{stdout}{Environment.NewLine}{stderr}";
    }
}
