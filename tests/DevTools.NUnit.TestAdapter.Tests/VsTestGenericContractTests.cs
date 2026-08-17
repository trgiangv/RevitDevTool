using DevTools.NUnit.Runtime.Fixtures;
using DevTools.NUnit.TestAdapter;
using DevTools.NUnit.TestAdapter.Runner;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Transport;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace DevTools.NUnit.TestAdapter.Tests;

public sealed class VsTestGenericContractTests
{
    const string MissingRunnerPath = @"C:\missing-devtools\DevTools.TestRunner.exe";
    const string MissingHostPath = @"C:\missing-autodesk\Revit.exe";

    [Fact]
    public void DiscoverTests_succeeds_when_runner_and_host_executables_are_missing()
    {
        Assert.False(File.Exists(MissingRunnerPath));
        Assert.False(File.Exists(MissingHostPath));

        AdapterSettings.Reset();
        var sink = new CollectingSink();
        var logger = new CollectingLogger();
        var source = typeof(FullSemanticsFixture).Assembly.Location;

        new DevToolsNUnitDiscoverer().DiscoverTests(
            [source],
            new FakeDiscoveryContext(CreateSettingsXml(MissingRunnerPath)),
            logger,
            sink);

        Assert.Empty(logger.Errors);
        Assert.Contains(sink.Tests, test => test.FullyQualifiedName.Contains("PlainTest_Passes", StringComparison.Ordinal));
        Assert.All(
            sink.Tests,
            test => Assert.Equal(
                DevToolsNUnitConstants.ExecutorUri,
                test.ExecutorUri.OriginalString,
                StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void RunTests_sends_nunit_framework_id_to_generic_transport()
    {
        AdapterSettings.Reset();
        var transport = new FakeTestRunnerTransport();
        var source = typeof(FullSemanticsFixture).Assembly.Location;
        var test = VsTestCaseMapper.ToTestCase(
            new RemoteTestCase("local:FullSemanticsFixture.PlainTest_Passes", "PlainTest_Passes", "FullSemanticsFixture.PlainTest_Passes", source));

        new DevToolsNUnitExecutor(transport).RunTests(
            [test],
            new FakeRunContext(CreateSettingsXml(MissingRunnerPath)),
            new CollectingHandle());

        Assert.NotNull(transport.LastRequest);
        Assert.Equal(TestingFrameworkIds.NUnit, transport.LastRequest!.FrameworkId);
        Assert.Equal(TestingProtocol.CurrentVersion, transport.LastRequest.ProtocolVersion);
        Assert.Equal(["FullSemanticsFixture.PlainTest_Passes"], transport.LastRequest.Selection.TestIds.ToArray());
        Assert.Equal(MissingRunnerPath, transport.LastHostOptions!.RunnerPath);
    }

    static string CreateSettingsXml(string runnerPath) =>
        $"""
        <RunSettings>
          <DevToolsNUnit>
            <HostName>Revit</HostName>
            <HostVersion>2025</HostVersion>
            <HostLaunch>false</HostLaunch>
            <HostTimeout>60</HostTimeout>
            <HostLaunchTimeout>180</HostLaunchTimeout>
            <RunnerPath>{runnerPath}</RunnerPath>
          </DevToolsNUnit>
        </RunSettings>
        """;

    sealed class FakeRunSettings(string xml) : IRunSettings
    {
        public string SettingsXml { get; } = xml;

        public ISettingsProvider? GetSettings(string? settingsName) => null;
    }

    sealed class FakeDiscoveryContext(string xml) : IDiscoveryContext
    {
        public IRunSettings RunSettings { get; } = new FakeRunSettings(xml);
    }

    sealed class FakeRunContext(string xml) : IRunContext
    {
        public IRunSettings RunSettings { get; } = new FakeRunSettings(xml);

        public bool KeepAlive => false;

        public bool InIsolation => false;

        public bool IsDataCollectionEnabled => false;

        public bool IsBeingDebugged => false;

        public string? TestRunDirectory => null;

        public string? SolutionDirectory => null;

        public ITestCaseFilterExpression? GetTestCaseFilter(
            IEnumerable<string>? properties,
            Func<string, TestProperty?> propertyProvider) =>
            null;
    }

    sealed class CollectingSink : ITestCaseDiscoverySink
    {
        public List<TestCase> Tests { get; } = [];

        public void SendTestCase(TestCase discoveredTest) => Tests.Add(discoveredTest);
    }

    sealed class CollectingLogger : IMessageLogger
    {
        public List<string> Errors { get; } = [];

        public void SendMessage(TestMessageLevel testMessageLevel, string message)
        {
            if (testMessageLevel == TestMessageLevel.Error)
                Errors.Add(message);
        }
    }

    sealed class CollectingHandle : IFrameworkHandle
    {
        public bool EnableShutdownAfterTestRun { get; set; }

        public void RecordStart(TestCase testCase)
        {
        }

        public void RecordEnd(TestCase testCase, TestOutcome outcome)
        {
        }

        public void RecordResult(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult testResult)
        {
        }

        public void RecordAttachments(IList<AttachmentSet> attachmentSets)
        {
        }

        public void SendMessage(TestMessageLevel testMessageLevel, string message)
        {
        }

        public int LaunchProcessWithDebuggerAttached(
            string filePath,
            string? workingDirectory,
            string? arguments,
            IDictionary<string, string?>? environmentVariables) =>
            0;
    }

    sealed class FakeTestRunnerTransport : ITestRunnerTransport
    {
        public TestingRunRequest? LastRequest { get; private set; }

        public TestingHostOptions? LastHostOptions { get; private set; }

        public TestingRunResponse Run(
            TestingRunRequest request,
            TestingHostOptions hostOptions,
            Action<TestingCaseResult> onResult)
        {
            LastRequest = request;
            LastHostOptions = hostOptions;
            return new TestingRunResponse(
                request.RunId,
                request.FrameworkId,
                null,
                [],
                TestingCancellationState.None,
                null,
                null);
        }

        public void Cancel(Guid runId)
        {
        }

        public void Dispose()
        {
        }
    }
}
