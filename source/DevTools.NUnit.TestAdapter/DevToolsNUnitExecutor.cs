using DevTools.NUnit.TestAdapter.Runner;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace DevTools.NUnit.TestAdapter;

[ExtensionUri(DevToolsNUnitConstants.ExecutorUri)]
[UsedImplicitly]
public sealed class DevToolsNUnitExecutor : ITestExecutor
{
    private IRunnerClient? _client;
    private readonly object _clientLock = new();

    public void RunTests(IEnumerable<TestCase>? tests, IRunContext? runContext, IFrameworkHandle? frameworkHandle)
    {
        if (tests is null || runContext is null || frameworkHandle is null)
            return;

        AdapterSettings.Apply(runContext.RunSettings);
        if (!AdapterSettings.IsConfigured)
            return;

        var grouped = tests.GroupBy(test => test.Source).ToList();
        foreach (var group in grouped)
            RunSource(group.Key, group.ToList(), runContext, frameworkHandle);
    }

    public void RunTests(IEnumerable<string>? sources, IRunContext? runContext, IFrameworkHandle? frameworkHandle)
    {
        if (sources is null || runContext is null || frameworkHandle is null)
            return;

        AdapterSettings.Apply(runContext.RunSettings);
        if (!AdapterSettings.IsConfigured)
            return;

        foreach (var source in sources)
        {
            try
            {
                SetWorkingDirectory(source);
                var testCases = LocalNUnitTestDiscoverer.Discover(source)
                    .Select(VSTestCaseMapper.ToTestCase)
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
            _client?.Cancel();
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
        var options = settings.ToRunnerHostOptions();
        var filter = VSTestCaseMapper.BuildFilter(tests);

        foreach (var test in tests)
            frameworkHandle.RecordStart(test);

        try
        {
            // Host-process debugging is deferred; never forward VSTest debug intent.
            var result = GetClient().Run(source, filter, options, waitForDebugger: false);
            ReportResults(tests, result, frameworkHandle);
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
        RemoteRunResult result,
        IFrameworkHandle frameworkHandle)
    {
        var byName = requestedTests
            .GroupBy(test => test.DisplayName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var reported = new HashSet<string>(StringComparer.Ordinal);
        foreach (var remoteCase in result.Cases)
        {
            if (!byName.TryGetValue(remoteCase.Name, out var testCase))
                continue;

            frameworkHandle.RecordResult(VSTestCaseMapper.ToTestResult(testCase, remoteCase));
            reported.Add(remoteCase.Name);
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

    private IRunnerClient GetClient()
    {
        lock (_clientLock)
            return _client ??= RunnerClientFactory.Create();
    }

    private static void SetWorkingDirectory(string source)
    {
        var directory = Path.GetDirectoryName(source);
        if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            Directory.SetCurrentDirectory(directory);
    }
}
