using DevTools.Testing.Abstractions.Contracts;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using TestAttachment = DevTools.Testing.Abstractions.Contracts.TestAttachment;
using TestCaseResult = DevTools.Testing.Abstractions.Contracts.TestCaseResult;

namespace DevTools.NUnit.Runtime;

internal static class NUnitResultMapper
{
    public static IReadOnlyList<TestCaseResult> MapRunResults(
        ITestResult root,
        NUnitSourceLocationProvider? sourceLocationProvider)
    {
        var cases = new List<TestCaseResult>();
        CollectCaseResults(root, sourceLocationProvider, cases);
        return cases;
    }

    public static TestCaseResult MapCaseResult(
        ITestResult result,
        NUnitSourceLocationProvider? sourceLocationProvider)
    {
        var test = result.Test;
        return new TestCaseResult(
            NUnitTestIdentity.Id(test),
            test.Name,
            MapOutcome(result.ResultState),
            result.Duration * 1000.0,
            MapMessage(result),
            result.StackTrace,
            string.IsNullOrWhiteSpace(result.Output) ? null : result.Output,
            MapSource(test, sourceLocationProvider),
            MapTraits(test),
            MapAttachments(result),
            NUnitTestIdentity.ParentId(test),
            test.FullName,
            MapSkipReason(test, result.ResultState));
    }

    internal static IReadOnlyList<TestAttachment> MapAttachments(ITestResult result)
    {
        if (result.TestAttachments.Count == 0)
            return [];

        var attachments = new List<TestAttachment>();
        foreach (var attachment in result.TestAttachments)
        {
            attachments.Add(new TestAttachment(
                attachment.FilePath,
                attachment.Description ?? Path.GetFileName(attachment.FilePath)));
        }

        return attachments;
    }

    private static void CollectCaseResults(
        ITestResult result,
        NUnitSourceLocationProvider? sourceLocationProvider,
        List<TestCaseResult> cases)
    {
        if (!result.Test.IsSuite)
        {
            cases.Add(MapCaseResult(result, sourceLocationProvider));
            return;
        }

        foreach (var child in result.Children)
            CollectCaseResults(child, sourceLocationProvider, cases);
    }

    internal static string MapOutcome(ResultState resultState)
    {
        if (resultState == ResultState.Success || resultState == ResultState.Warning)
            return TestOutcomes.Passed;

        if (resultState == ResultState.Inconclusive)
            return TestOutcomes.Inconclusive;

        if (resultState == ResultState.Cancelled)
            return TestOutcomes.Cancelled;

        if (resultState == ResultState.Ignored || resultState == ResultState.Explicit || resultState == ResultState.Skipped)
            return TestOutcomes.Skipped;

        if (resultState == ResultState.Error
            || resultState == ResultState.SetUpError
            || resultState == ResultState.TearDownError
            || resultState == ResultState.NotRunnable)
            return TestOutcomes.Error;

        return resultState.Status switch
        {
            TestStatus.Failed => TestOutcomes.Failed,
            TestStatus.Skipped => TestOutcomes.Skipped,
            TestStatus.Inconclusive => TestOutcomes.Inconclusive,
            TestStatus.Passed or TestStatus.Warning => TestOutcomes.Passed,
            _ => TestOutcomes.Failed
        };

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

    private static IReadOnlyList<TestTrait> MapTraits(ITest test)
    {
        var traits = new List<TestTrait>();
        AppendPropertyTraits(test.Properties, traits);

        if (traits.Count == 0)
            return [];

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

    private static void AppendPropertyTraits(IPropertyBag properties, List<TestTrait> traits)
    {
        foreach (var key in properties.Keys)
        {
            if (!IsPublicTrait(key))
                continue;

            if (!properties.TryGet(key, out var values))
                continue;

            foreach (var value in values)
            {
                if (value is null)
                    continue;

                traits.Add(new TestTrait(
                    NormalizeTraitName(key),
                    Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty));
            }
        }
    }

    private static string NormalizeTraitName(string key) =>
        string.Equals(key, PropertyNames.Category, StringComparison.Ordinal) ? "Category" : key;

    internal static TestSourceLocation? MapSource(
        ITest test,
        NUnitSourceLocationProvider? sourceLocationProvider)
    {
        if (sourceLocationProvider is null)
            return null;

        if (!sourceLocationProvider.TryGetSourceLocation(test, out var filePath, out var lineNumber))
            return null;

        return new TestSourceLocation(filePath!, lineNumber);
    }
}
