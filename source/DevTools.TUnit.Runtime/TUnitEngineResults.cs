using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Runtime;
using DevTools.Testing.Mtp;
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
            var mapped = MtpNodeResults.Map(node, runCancelled: false);
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

    internal static bool IsTerminal(TestNode node) => MtpNodeResults.HasTerminalState(node);

    internal static string? FrameworkOutput(TestNode node) => MtpNodeResults.FrameworkOutput(node);
}