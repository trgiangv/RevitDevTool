using DevTools.NUnit.Provider;
using DevTools.NUnit.TestAdapter.Runner;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Transport;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace DevTools.NUnit.TestAdapter;

[ExtensionUri(DevToolsNUnitConstants.ExecutorUri)]
[UsedImplicitly]
public sealed class DevToolsNUnitExecutor : ITestExecutor
{
    private ITestRunnerTransport? _client;
    private readonly object _clientLock = new();

    public DevToolsNUnitExecutor()
    {
    }

    internal DevToolsNUnitExecutor(ITestRunnerTransport transport)
    {
        _client = transport ?? throw new ArgumentNullException(nameof(transport));
    }

    public void RunTests(IEnumerable<TestCase>? tests, IRunContext? runContext, IFrameworkHandle? frameworkHandle)
    {
        if (tests is null || runContext is null || frameworkHandle is null)
            return;

        AdapterSettings.Apply(runContext.RunSettings);

        var grouped = tests.GroupBy(test => test.Source).ToList();
        foreach (var group in grouped)
        {
            AdapterSettings.TryApplyFromAssembly(group.Key);
            if (!AdapterSettings.IsConfigured)
                continue;

            RunSource(group.Key, group.ToList(), runContext, frameworkHandle);
        }
    }

    public void RunTests(IEnumerable<string>? sources, IRunContext? runContext, IFrameworkHandle? frameworkHandle)
    {
        if (sources is null || runContext is null || frameworkHandle is null)
            return;

        AdapterSettings.Apply(runContext.RunSettings);

        foreach (var source in sources)
        {
            try
            {
                AdapterSettings.TryApplyFromAssembly(source);
                if (!AdapterSettings.IsConfigured)
                    continue;

                SetWorkingDirectory(source);
                var testCases = LocalNUnitTestDiscoverer.Discover(source)
                    .Select(VsTestCaseMapper.ToTestCase)
                    .Where(test => MatchesFilter(test, runContext))
                    .ToList();
                RunSource(source, testCases, runContext, frameworkHandle);
            }
            catch (Exception ex)
            {
                frameworkHandle.SendMessage(TestMessageLevel.Error, $"DevTools.NUnit run failed for '{source}': {ex.Message}");
            }
        }
    }

    public void Cancel()
    {
        lock (_clientLock)
            _client?.Cancel(Guid.Empty);
    }

    private void RunSource(
        string source,
        IReadOnlyList<TestCase> tests,
        IRunContext runContext,
        IFrameworkHandle frameworkHandle)
    {
        if (tests.Count == 0)
            return;

        var settings = AdapterSettings.Current;
        var hostOptions = settings.ToTestingHostOptions(
            runContext.IsBeingDebugged ? Environment.ProcessId : null);
        var request = new TestingRunRequest(
            TestingProtocol.CurrentVersion,
            Guid.NewGuid(),
            "nunit",
            new TestingAssemblyReference(source, null, null),
            NUnitTestingMapping.ToSelection(VsTestCaseMapper.BuildFilter(tests)),
            new Dictionary<string, string>());

        foreach (var test in tests)
            frameworkHandle.RecordStart(test);

        try
        {
            var result = GetClient().Run(request, hostOptions, _ => { });
            ReportResults(tests, result.Results, frameworkHandle);
        }
        catch (Exception ex)
        {
            foreach (var test in tests)
            {
                frameworkHandle.RecordResult(new TestResult(test)
                {
                    Outcome = TestOutcome.Failed,
                    ErrorMessage = ex.Message,
                });
            }
        }
    }

    private static void ReportResults(
        IReadOnlyList<TestCase> requestedTests,
        IReadOnlyList<TestingCaseResult> result,
        IFrameworkHandle frameworkHandle)
    {
        var byName = requestedTests
            .GroupBy(test => test.DisplayName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var reported = new HashSet<string>(StringComparer.Ordinal);
        foreach (var remoteCase in result)
        {
            if (!byName.TryGetValue(remoteCase.DisplayName, out var testCase))
                continue;

            frameworkHandle.RecordResult(VsTestCaseMapper.ToTestResult(testCase, remoteCase));
            reported.Add(remoteCase.DisplayName);
        }

        foreach (var test in requestedTests)
        {
            if (reported.Contains(test.DisplayName))
                continue;

            frameworkHandle.RecordResult(new TestResult(test)
            {
                Outcome = TestOutcome.Failed,
                ErrorMessage = "No result was returned for this test by the host runner.",
            });
        }
    }

    private ITestRunnerTransport GetClient()
    {
        lock (_clientLock)
            return _client ??= RunnerClientFactory.Create();
    }

    private static bool MatchesFilter(TestCase test, IRunContext runContext)
    {
        var filter = runContext.GetTestCaseFilter(FilterPropertyNames, ResolveFilterProperty);
        if (filter is null)
            return true;

        return filter.MatchTestCase(test, name => GetFilterPropertyValue(test, name));
    }

    private static readonly string[] FilterPropertyNames =
    [
        "FullyQualifiedName",
        "DisplayName",
        "Name",
    ];

    private static TestProperty? ResolveFilterProperty(string name) =>
        name.ToUpperInvariant() switch
        {
            "FULLYQUALIFIEDNAME" => TestCaseProperties.FullyQualifiedName,
            "DISPLAYNAME" or "NAME" => TestCaseProperties.DisplayName,
            _ => null,
        };

    private static object? GetFilterPropertyValue(TestCase test, string name) =>
        name.ToUpperInvariant() switch
        {
            "FULLYQUALIFIEDNAME" => test.FullyQualifiedName,
            "DISPLAYNAME" or "NAME" => test.DisplayName,
            _ => null,
        };

    private static void SetWorkingDirectory(string source)
    {
        var directory = Path.GetDirectoryName(source);
        if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            Directory.SetCurrentDirectory(directory);
    }
}
