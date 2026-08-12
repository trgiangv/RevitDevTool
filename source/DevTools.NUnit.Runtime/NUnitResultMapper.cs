using DevTools.NUnit.Core.Contracts;
using DevTools.NUnit.Core.Results;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace DevTools.NUnit.Runtime;

internal static class NUnitResultMapper
{
    public static IReadOnlyList<NUnitDiscoveredTest> MapDiscovery(
        ITest root,
        NUnitTestIdentityRegistry identityRegistry,
        NUnitSourceLocationProvider? sourceLocationProvider)
    {
        var cases = new List<NUnitDiscoveredTest>();
        CollectDiscoveredTests(root, identityRegistry, sourceLocationProvider, cases);
        return cases;
    }

    public static NUnitRunSummary MapSummary(IReadOnlyList<NUnitCaseResult> cases)
    {
        var passed = 0;
        var failed = 0;
        var skipped = 0;
        var inconclusive = 0;
        var errors = 0;
        var cancelled = 0;

        foreach (var testCase in cases)
        {
            switch (testCase.Outcome)
            {
                case NUnitOutcomes.Passed:
                    passed++;
                    break;
                case NUnitOutcomes.Failed:
                    failed++;
                    break;
                case NUnitOutcomes.Skipped:
                    skipped++;
                    break;
                case NUnitOutcomes.Inconclusive:
                    inconclusive++;
                    break;
                case NUnitOutcomes.Error:
                    errors++;
                    break;
                case NUnitOutcomes.Cancelled:
                    cancelled++;
                    break;
            }
        }

        return new NUnitRunSummary(passed, failed, skipped, inconclusive, errors, cancelled);
    }

    public static IReadOnlyList<NUnitCaseResult> MapRunResults(
        ITestResult root,
        NUnitTestIdentityRegistry identityRegistry,
        NUnitSourceLocationProvider? sourceLocationProvider)
    {
        var cases = new List<NUnitCaseResult>();
        CollectCaseResults(root, identityRegistry, sourceLocationProvider, cases);
        return cases;
    }

    public static NUnitCaseResult MapCaseResult(
        ITestResult result,
        NUnitTestIdentityRegistry identityRegistry,
        NUnitSourceLocationProvider? sourceLocationProvider)
    {
        var test = result.Test;
        return new NUnitCaseResult(
            NUnitTestIdentityMapper.MapTestId(test, identityRegistry),
            test.Name,
            MapOutcome(result.ResultState),
            result.Duration * 1000.0,
            MapMessage(result),
            result.StackTrace,
            string.IsNullOrWhiteSpace(result.Output) ? null : result.Output,
            NUnitTestIdentityMapper.MapParentTestId(test, identityRegistry),
            MapTraits(test),
            MapSource(test, sourceLocationProvider),
            MapSkipReason(test, result.ResultState),
            MapAttachments(result));
    }

    public static NUnitDiscoveredTest MapDiscoveredTest(
        ITest test,
        NUnitTestIdentityRegistry identityRegistry,
        NUnitSourceLocationProvider? sourceLocationProvider)
    {
        return new NUnitDiscoveredTest(
            NUnitTestIdentityMapper.MapTestId(test, identityRegistry),
            test.Name,
            test.FullName,
            NUnitTestIdentityMapper.MapParentTestId(test, identityRegistry),
            MapTraits(test),
            MapSource(test, sourceLocationProvider),
            MapSkipReason(test, null));
    }

    internal static IReadOnlyList<NUnitAttachment>? MapAttachments(ITestResult result)
    {
        if (result.TestAttachments.Count == 0)
            return null;

        var attachments = new List<NUnitAttachment>();
        foreach (TestAttachment attachment in result.TestAttachments)
        {
            attachments.Add(new NUnitAttachment(
                attachment.Description ?? Path.GetFileName(attachment.FilePath),
                null,
                attachment.FilePath,
                null));
        }

        return attachments;
    }

    private static void CollectDiscoveredTests(
        ITest test,
        NUnitTestIdentityRegistry identityRegistry,
        NUnitSourceLocationProvider? sourceLocationProvider,
        List<NUnitDiscoveredTest> cases)
    {
        if (!test.IsSuite)
        {
            cases.Add(MapDiscoveredTest(test, identityRegistry, sourceLocationProvider));
            return;
        }

        var children = test.Tests;
        for (var index = 0; index < children.Count; index++)
            CollectDiscoveredTests(children[index], identityRegistry, sourceLocationProvider, cases);
    }

    private static void CollectCaseResults(
        ITestResult result,
        NUnitTestIdentityRegistry identityRegistry,
        NUnitSourceLocationProvider? sourceLocationProvider,
        List<NUnitCaseResult> cases)
    {
        if (!result.Test.IsSuite)
        {
            cases.Add(MapCaseResult(result, identityRegistry, sourceLocationProvider));
            return;
        }

        foreach (var child in result.Children)
            CollectCaseResults(child, identityRegistry, sourceLocationProvider, cases);
    }

    internal static string MapOutcome(ResultState resultState)
    {
        if (resultState == ResultState.Success || resultState == ResultState.Warning)
            return NUnitOutcomes.Passed;

        if (resultState == ResultState.Inconclusive)
            return NUnitOutcomes.Inconclusive;

        if (resultState == ResultState.Cancelled)
            return NUnitOutcomes.Cancelled;

        if (resultState == ResultState.Ignored || resultState == ResultState.Explicit || resultState == ResultState.Skipped)
            return NUnitOutcomes.Skipped;

        if (resultState == ResultState.Error
            || resultState == ResultState.SetUpError
            || resultState == ResultState.TearDownError
            || resultState == ResultState.NotRunnable)
            return NUnitOutcomes.Error;

        if (resultState.Status == TestStatus.Failed)
            return NUnitOutcomes.Failed;

        if (resultState.Status == TestStatus.Skipped)
            return NUnitOutcomes.Skipped;

        if (resultState.Status == TestStatus.Inconclusive)
            return NUnitOutcomes.Inconclusive;

        if (resultState.Status == TestStatus.Passed || resultState.Status == TestStatus.Warning)
            return NUnitOutcomes.Passed;

        return NUnitOutcomes.Failed;
    }

    private static string? MapMessage(ITestResult result)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(result.Message))
            parts.Add(result.Message);

        foreach (AssertionResult assertion in result.AssertionResults)
        {
            if (assertion.Status != AssertionStatus.Warning || string.IsNullOrWhiteSpace(assertion.Message))
                continue;

            if (parts.Any(existing => string.Equals(existing, assertion.Message, StringComparison.Ordinal)))
                continue;

            parts.Add(assertion.Message);
        }

        return parts.Count == 0 ? null : string.Join(Environment.NewLine, parts);
    }

    private static string? MapSkipReason(ITest test, ResultState? resultState)
    {
        var reason = test.Properties.Get(PropertyNames.SkipReason) as string;
        if (!string.IsNullOrWhiteSpace(reason))
            return reason;

        if (resultState == ResultState.Ignored)
            return test.Properties.Get(PropertyNames.SkipReason) as string ?? "Ignored";

        if (resultState == ResultState.Explicit)
            return test.Properties.Get(PropertyNames.SkipReason) as string ?? "Explicit";

        return test.RunState switch
        {
            RunState.Ignored => test.Properties.Get(PropertyNames.SkipReason) as string ?? "Ignored",
            RunState.Explicit => test.Properties.Get(PropertyNames.SkipReason) as string ?? "Explicit",
            RunState.Skipped => test.Properties.Get(PropertyNames.SkipReason) as string,
            _ => null,
        };
    }

    private static IReadOnlyList<NUnitTrait>? MapTraits(ITest test)
    {
        var traits = new List<NUnitTrait>();
        AppendPropertyTraits(test.Properties, traits);

        if (traits.Count == 0)
            return null;

        return traits;
    }

    private static bool IsPublicTrait(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        if (key.StartsWith('_'))
            return false;

        return key switch
        {
            PropertyNames.AppDomain => false,
            PropertyNames.JoinType => false,
            PropertyNames.ProcessId => false,
            PropertyNames.ProviderStackTrace => false,
            PropertyNames.SkipReason => false,
            PropertyNames.RepeatCount => false,
            PropertyNames.Order => false,
            PropertyNames.LevelOfParallelism => false,
            PropertyNames.ParallelScope => false,
            PropertyNames.Timeout => false,
            PropertyNames.MaxTime => false,
            PropertyNames.ApartmentState => false,
            PropertyNames.RequiresThread => false,
            PropertyNames.SetCulture => false,
            PropertyNames.SetUICulture => false,
            PropertyNames.UseCancellation => false,
            PropertyNames.UnhandledExceptionHandling => false,
            PropertyNames.NoTests => false,
            _ => true,
        };
    }

    private static void AppendPropertyTraits(IPropertyBag properties, List<NUnitTrait> traits)
    {
        foreach (var key in properties.Keys)
        {
            if (!IsPublicTrait(key))
                continue;

            if (!properties.TryGet(key, out var values) || values is null)
                continue;

            foreach (var value in values)
            {
                if (value is null)
                    continue;

                traits.Add(new NUnitTrait(
                    NormalizeTraitName(key),
                    Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty));
            }
        }
    }

    private static string NormalizeTraitName(string key) =>
        string.Equals(key, PropertyNames.Category, StringComparison.Ordinal) ? "Category" : key;

    private static NUnitSourceLocation? MapSource(
        ITest test,
        NUnitSourceLocationProvider? sourceLocationProvider)
    {
        if (sourceLocationProvider is null)
            return null;

        if (!sourceLocationProvider.TryGetSourceLocation(test, out var filePath, out var lineNumber))
            return null;

        return new NUnitSourceLocation(filePath!, lineNumber);
    }
}
