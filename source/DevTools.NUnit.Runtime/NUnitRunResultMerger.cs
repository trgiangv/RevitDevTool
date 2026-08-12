using DevTools.NUnit.Core.Contracts;

namespace DevTools.NUnit.Runtime;

internal static class NUnitRunResultMerger
{
    public static IReadOnlyList<NUnitCaseResult> Merge(
        IReadOnlyList<NUnitCaseResult> frameworkCases,
        IReadOnlyList<NUnitCaseResult> abortedCases)
    {
        if (abortedCases.Count == 0)
            return frameworkCases;

        if (frameworkCases.Count == 0)
            return abortedCases;

        var merged = new List<NUnitCaseResult>(frameworkCases.Count + abortedCases.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < frameworkCases.Count; index++)
        {
            var frameworkCase = frameworkCases[index];
            merged.Add(frameworkCase);
            seen.Add(frameworkCase.Id);
        }

        for (var index = 0; index < abortedCases.Count; index++)
        {
            var abortedCase = abortedCases[index];
            if (seen.Add(abortedCase.Id))
                merged.Add(abortedCase);
        }

        return merged;
    }
}
