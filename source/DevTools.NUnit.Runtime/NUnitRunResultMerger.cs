using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.NUnit.Runtime;

internal static class NUnitRunResultMerger
{
    public static IReadOnlyList<TestingCaseResult> Merge(
        IReadOnlyList<TestingCaseResult> frameworkCases,
        IReadOnlyList<TestingCaseResult> abortedCases)
    {
        if (abortedCases.Count == 0)
            return frameworkCases;

        if (frameworkCases.Count == 0)
            return abortedCases;

        var merged = new List<TestingCaseResult>(frameworkCases.Count + abortedCases.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < frameworkCases.Count; index++)
        {
            var frameworkCase = frameworkCases[index];
            merged.Add(frameworkCase);
            seen.Add(frameworkCase.TestId);
        }

        for (var index = 0; index < abortedCases.Count; index++)
        {
            var abortedCase = abortedCases[index];
            if (seen.Add(abortedCase.TestId))
                merged.Add(abortedCase);
        }

        return merged;
    }
}
