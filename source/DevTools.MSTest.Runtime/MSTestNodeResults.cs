using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Runtime;
using DevTools.Testing.Mtp;
using Microsoft.Testing.Platform.Extensions.Messages;

namespace DevTools.MSTest.Runtime;

#pragma warning disable CS0618
#pragma warning disable MTP0001

internal readonly record struct MSTestCaseOutput(string? Console, string? Trace);

internal static class MSTestNodeResults
{
    public static IReadOnlyList<TestCaseResult> Map(
        IEnumerable<TestNode> nodes,
        bool runCancelled,
        IReadOnlyDictionary<string, MSTestCaseOutput>? capturedByUid = null)
    {
        var results = new List<TestCaseResult>();
        foreach (var node in nodes)
        {
            var mapped = MtpNodeResults.Map(node, runCancelled);
            if (mapped is null)
                continue;

            mapped = mapped with { FullName = MethodFullName(node) ?? mapped.TestId };
            if (capturedByUid is not null
                && capturedByUid.TryGetValue(node.Uid.Value, out var captured))
            {
                var console = string.IsNullOrWhiteSpace(mapped.Output) ? captured.Console : mapped.Output;
                mapped = mapped with { Output = TestRunTraceScope.Merge(console, captured.Trace) };
            }

            results.Add(mapped);
        }

        return results;
    }

    /// <summary>
    /// Human name for the result. The id the session filters with
    /// <c>--filter-uid</c> is <see cref="TestNode.Uid"/>.
    /// </summary>
    private static string? MethodFullName(TestNode node)
    {
        var identifier = node.Properties.SingleOrDefault<TestMethodIdentifierProperty>();
        if (identifier is null || string.IsNullOrEmpty(identifier.MethodName))
            return null;

        var className = string.IsNullOrEmpty(identifier.Namespace)
            ? identifier.TypeName
            : identifier.Namespace + "." + identifier.TypeName;
        return className + "." + identifier.MethodName;
    }
}