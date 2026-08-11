using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;

namespace DevTools.NUnit.TestAdapter;

internal static class VSTestCaseMapper
{
    private static readonly Uri ExecutorUri = new(DevToolsNUnitConstants.ExecutorUri);

    private static readonly TestProperty TestIdProperty = TestProperty.Register(
        DevToolsNUnitConstants.TestIdProperty,
        DevToolsNUnitConstants.TestIdProperty,
        typeof(string),
        typeof(VSTestCaseMapper));

    private static readonly TestProperty TestFullNameProperty = TestProperty.Register(
        DevToolsNUnitConstants.TestFullNameProperty,
        DevToolsNUnitConstants.TestFullNameProperty,
        typeof(string),
        typeof(VSTestCaseMapper));

    public static TestCase ToTestCase(Runner.RemoteTestCase test)
    {
        var testCase = new TestCase(test.FullName, ExecutorUri, test.Source)
        {
            DisplayName = test.Name,
            Id = EqtHash.GuidFromString(test.FullName),
        };

        testCase.SetPropertyValue(TestIdProperty, test.Id);
        testCase.SetPropertyValue(TestFullNameProperty, test.FullName);
        ApplySourceNavigation(testCase, test);
        return testCase;
    }

    public static string? BuildFilter(IEnumerable<TestCase> tests)
    {
        var clauses = tests
            .Select(test => test.GetPropertyValue<string>(TestFullNameProperty, null) ?? test.FullyQualifiedName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct()
            .Select(name => $"test == '{EscapeWhereLiteral(name!)}'")
            .ToList();

        return clauses.Count == 0 ? null : string.Join(" | ", clauses);
    }

    public static TestResult ToTestResult(TestCase testCase, Runner.RemoteTestCaseResult remoteCase)
    {
        var result = new TestResult(testCase)
        {
            Outcome = MapOutcome(remoteCase.Outcome),
            Duration = TimeSpan.FromMilliseconds(remoteCase.DurationMilliseconds),
            ErrorMessage = remoteCase.Message,
            ErrorStackTrace = remoteCase.StackTrace,
        };

        if (!string.IsNullOrWhiteSpace(remoteCase.Output))
            result.Messages.Add(new TestResultMessage(TestResultMessage.StandardOutCategory, remoteCase.Output));

        return result;
    }

    private static void ApplySourceNavigation(TestCase testCase, Runner.RemoteTestCase test)
    {
        if (!AdapterSettings.Current.CollectSourceInformation)
            return;

        var navigation = PortablePdbNavigationCache.GetOrAdd(test.Source);
        if (!navigation.TryGetNavigationData(test.FullName, out var filePath, out var lineNumber))
            return;

        testCase.CodeFilePath = filePath;
        testCase.LineNumber = lineNumber;
    }

    private static TestOutcome MapOutcome(string outcome) =>
        outcome switch
        {
            "Passed" => TestOutcome.Passed,
            "Failed" => TestOutcome.Failed,
            "Skipped" => TestOutcome.Skipped,
            "Inconclusive" or "Cancelled" => TestOutcome.None,
            _ => TestOutcome.Failed,
        };

    private static string EscapeWhereLiteral(string value) => value.Replace("'", "''");
}
