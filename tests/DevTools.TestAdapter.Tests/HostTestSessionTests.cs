using DevTools.TestAdapter;
using DevTools.Testing.Abstractions;
using DevTools.Testing.Abstractions.Config;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Transport;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Requests;

namespace DevTools.TestAdapter.Tests;

[Collection(nameof(AdapterHostTestDiscoveryCollection))]
public sealed class HostTestSessionTests
{
    private static readonly object DiscoveryProviderLock = new();

    [Fact]
    public void ScaleForRun_multiplies_per_test_timeout()
    {
        var options = new TestingHostOptions("Revit", "2026", false, 60, 180, @"C:\Runner.exe");
        var scaled = HostTestFramework.ScaleForRun(options, testCount: 3);
        Assert.Equal(180, scaled.PerTestTimeoutSeconds);
        Assert.Equal(60, options.PerTestTimeoutSeconds);
    }

    [Fact]
    public void SelectCases_throws_when_mtp_plugin_is_not_registered()
    {
        lock (DiscoveryProviderLock)
        {
            var previous = HostTestDiscovery.Provider;
            var previousMapper = HostTestDiscovery.RunMapper;
            HostTestDiscovery.Provider = null;
            HostTestDiscovery.RunMapper = null;
            try
            {
                var ex = Assert.Throws<InvalidOperationException>(
                    () => HostTestFramework.SelectCases(
                        typeof(HostTestSessionTests).Assembly.Location,
                        new TestingSelection([])));
                Assert.Contains("mtpAssembly", ex.Message, StringComparison.Ordinal);
            }
            finally
            {
                HostTestDiscovery.Provider = previous;
                HostTestDiscovery.RunMapper = previousMapper;
            }
        }
    }

    [Fact]
    public void Run_returns_pass_fail_skip_and_error_from_transport()
    {
        var transport = new FakeTestRunnerTransport
        {
            Response = new TestingRunResponse(
                Guid.NewGuid(),
                "nunit",
                "gen",
                [
                    new TestingCaseResult("1", "Pass", "Passed", 10, null, null, null, null, [], []),
                    new TestingCaseResult("2", "Fail", "Failed", 20, "boom", "at Foo", null, null, [], []),
                    new TestingCaseResult("3", "Skip", "Skipped", 0, "ignored", null, null, null, [], []),
                    new TestingCaseResult("4", "Err", "Error", 5, "init", null, null, null, [], []),
                ],
                TestingCancellationState.None,
                null,
                null),
        };
        var session = new HostTestSession(transport);

        var response = session.Run(
            "C:\\tests\\a.dll",
            new TestingHostOptions("Revit", "2026", false, 60, 180, @"C:\Runner.exe", FrameworkId: "nunit"),
            new TestingSelection([], null));

        Assert.Equal("nunit", transport.LastRequest!.FrameworkId);
        Assert.Equal(["Passed", "Failed", "Skipped", "Error"], response.Results.Select(result => result.Outcome).ToArray());
    }

    [Fact]
    public void DiscoverNodes_completes_when_runner_path_cannot_be_read()
    {
        var runnerPath = Path.Combine(Path.GetTempPath(), "devtools-mtp-locked-runner-" + Guid.NewGuid().ToString("N") + ".exe");
        File.WriteAllBytes(runnerPath, [0x4D, 0x5A]);
        try
        {
            using (new FileStream(runnerPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                lock (DiscoveryProviderLock)
                {
                    var previous = HostTestDiscovery.Provider;
                    var previousMapper = HostTestDiscovery.RunMapper;
                    HostTestDiscovery.Provider = new StubHostTestDiscoverer();
                    HostTestDiscovery.RunMapper = null;
                    try
                    {
                        var nodes = HostTestFramework.DiscoverNodes(
                            typeof(HostTestSessionTests).Assembly.Location,
                            new TestingSelection([]));
                        Assert.NotNull(nodes);
                        Assert.NotEmpty(nodes);
                    }
                    finally
                    {
                        HostTestDiscovery.Provider = previous;
                        HostTestDiscovery.RunMapper = previousMapper;
                    }
                }
            }
        }
        finally
        {
            try { File.Delete(runnerPath); } catch { /* best effort */ }
        }
    }

    [Fact]
    public void Run_sends_nunit_framework_id_to_generic_transport()
    {
        var transport = new FakeTestRunnerTransport();
        var session = new HostTestSession(transport);

        session.Run(
            "C:\\tests\\a.dll",
            new TestingHostOptions("Revit", "2026", false, 60, 180, @"C:\missing-devtools-testrunner.exe", FrameworkId: "nunit"),
            new TestingSelection(["HostSmokeTests.Arithmetic"], null));

        Assert.Equal("nunit", transport.LastRequest!.FrameworkId);
        Assert.Equal(["HostSmokeTests.Arithmetic"], transport.LastRequest.Selection.TestIds.ToArray());
    }

    [Fact]
    public void Run_throws_when_framework_id_is_empty()
    {
        var session = new HostTestSession(new FakeTestRunnerTransport());
        var ex = Assert.Throws<InvalidOperationException>(() => session.Run(
            "C:\\tests\\a.dll",
            new TestingHostOptions("Revit", "2026", false, 60, 180, @"C:\Runner.exe", FrameworkId: ""),
            new TestingSelection([])));
        Assert.Contains("frameworkId", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Name_filter_round_trips_through_generic_selection()
    {
        var selection = HostTestFramework.ToRunnerFilter(null, nameFilter: "Arithmetic_runs_inside_host");

        Assert.Empty(selection.TestIds);
        Assert.Equal(["Arithmetic_runs_inside_host"], selection.Names!.ToArray());
        Assert.Null(selection.ProviderPayload);
    }
}

public sealed class ProcessTestRunnerCliTests
{
    [Fact]
    public void BuildRunArguments_sends_framework_and_test_tokens()
    {
        var args = TestingRunnerCli.BuildRunArguments(
            new TestingRunRequest(
                TestingProtocol.CurrentVersion,
                Guid.NewGuid(),
                "nunit",
                new TestingAssemblyReference(@"C:\tests\HostTests.dll", null, null),
                new TestingSelection(["HostSmokeTests.Arithmetic"]),
                new Dictionary<string, string>()),
            new TestingHostOptions("Revit", "2026", true, 60, 180, @"C:\Runner.exe"));

        Assert.Equal("run", args[0]);
        Assert.Contains("--framework", args);
        Assert.Contains("nunit", args);
        Assert.Contains(@"C:\tests\HostTests.dll", args);
        Assert.Contains("--host", args);
        Assert.Contains("Revit", args);
        Assert.Contains("--force-launch", args);
        Assert.Contains("--test", args);
        Assert.Contains("""["HostSmokeTests.Arithmetic"]""", args);
        Assert.DoesNotContain("--name", args);
        Assert.DoesNotContain("discover", args);
    }

    [Fact]
    public void BuildRunArguments_run_adds_debug_flags_when_parent_pid_is_set()
    {
        var args = TestingRunnerCli.BuildRunArguments(
            new TestingRunRequest(
                TestingProtocol.CurrentVersion,
                Guid.NewGuid(),
                "nunit",
                new TestingAssemblyReference(@"C:\tests\HostTests.dll", null, null),
                new TestingSelection([]),
                new Dictionary<string, string>()),
            new TestingHostOptions("Revit", "2026", false, 60, 180, @"C:\Runner.exe", DebugParentPid: 4242));

        Assert.DoesNotContain("--debug", args);
        Assert.Contains("--debug-parent-pid", args);
        Assert.Contains("4242", args);
    }

    [Fact]
    public void BuildRunArguments_omits_debug_flags_when_parent_pid_is_absent()
    {
        var args = TestingRunnerCli.BuildRunArguments(
            new TestingRunRequest(
                TestingProtocol.CurrentVersion,
                Guid.NewGuid(),
                "nunit",
                new TestingAssemblyReference(@"C:\tests\HostTests.dll", null, null),
                new TestingSelection([]),
                new Dictionary<string, string>()),
            new TestingHostOptions("Revit", "2026", false, 60, 180, @"C:\Runner.exe"));

        Assert.DoesNotContain("--debug", args);
        Assert.DoesNotContain("--debug-parent-pid", args);
        Assert.DoesNotContain("4242", args);
    }
}

public sealed class HostOptionsLoaderTests
{
    [Fact]
    public void Load_reads_mtp_iconfiguration_keys()
    {
        IConfiguration configuration = new StubConfiguration(new Dictionary<string, string?>
        {
            [HostTestConfig.Keys.Configuration(HostTestConfig.Keys.HostName)] = "Civil3D",
            [HostTestConfig.Keys.Configuration(HostTestConfig.Keys.HostVersion)] = "2026",
            [HostTestConfig.Keys.Configuration(HostTestConfig.Keys.ForceLaunch)] = "true",
            [HostTestConfig.Keys.Configuration(HostTestConfig.Keys.PerTestTimeoutSeconds)] = "90",
            [HostTestConfig.Keys.Configuration(HostTestConfig.Keys.LaunchTimeoutSeconds)] = "240",
            [HostTestConfig.Keys.Configuration(HostTestConfig.Keys.RunnerPath)] = @"C:\Runner.exe",
            [HostTestConfig.Keys.Configuration(HostTestConfig.Keys.FrameworkId)] = "nunit",
        });

        var options = HostOptionsLoader.Load(configuration);

        Assert.Equal("Civil3D", options.HostName);
        Assert.Equal("2026", options.HostVersion);
        Assert.True(options.ForceLaunch);
        Assert.Equal(90, options.PerTestTimeoutSeconds);
        Assert.Equal(240, options.LaunchTimeoutSeconds);
        Assert.Equal("nunit", options.FrameworkId);
        Assert.Equal(@"C:\Runner.exe", options.RunnerPath);
    }

    [Fact]
    public void Load_throws_when_framework_id_is_empty()
    {
        IConfiguration configuration = new StubConfiguration(new Dictionary<string, string?>
        {
            [HostTestConfig.Keys.Configuration(HostTestConfig.Keys.HostName)] = "Revit",
            [HostTestConfig.Keys.Configuration(HostTestConfig.Keys.HostVersion)] = "2026",
            [HostTestConfig.Keys.Configuration(HostTestConfig.Keys.PerTestTimeoutSeconds)] = "60",
            [HostTestConfig.Keys.Configuration(HostTestConfig.Keys.LaunchTimeoutSeconds)] = "180",
            [HostTestConfig.Keys.Configuration(HostTestConfig.Keys.FrameworkId)] = "",
        });

        var ex = Assert.Throws<InvalidOperationException>(() => HostOptionsLoader.Load(configuration));
        Assert.Contains("frameworkId", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_throws_when_devtools_section_is_missing()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => HostOptionsLoader.Load(new StubConfiguration(new Dictionary<string, string?>())));
        Assert.Contains(HostTestConfig.FileName, ex.Message, StringComparison.Ordinal);
        Assert.Contains(HostTestConfig.SectionName, ex.Message, StringComparison.Ordinal);
    }
}

public sealed class TestNodeMapperTests
{
    [Fact]
    public void CreateErrorNode_sets_error_state()
    {
        var node = TestNodeProperties.CreateErrorNode("uid", "display", new InvalidOperationException("boom"));
        Assert.Equal("uid", node.Uid.Value);
        Assert.Equal("display", node.DisplayName);
        Assert.NotNull(node.Properties.SingleOrDefault<ErrorTestNodeStateProperty>());
    }

    [Fact]
    public void ToDiscoveredNode_uses_test_id_as_uid()
    {
        var node = HostTestFramework.ToDiscoveredNode(
            new TestingDiscoveredTest(
                "HostSmokeTests.Arithmetic",
                "Arithmetic",
                "HostSmokeTests.Arithmetic",
                "HostSmokeTests",
                "Arithmetic",
                Namespace: "",
                TypeName: "HostSmokeTests"));

        Assert.Equal("HostSmokeTests.Arithmetic", node.Uid.Value);
        Assert.Equal("Arithmetic", node.DisplayName);
        Assert.NotNull(node.Properties.SingleOrDefault<DiscoveredTestNodeStateProperty>());
        var identity = node.Properties.Single<TestMethodIdentifierProperty>();
        Assert.Equal("HostSmokeTests", identity.TypeName);
        Assert.Equal("Arithmetic", identity.MethodName);
    }

    [Fact]
    public void ToDiscoveredNode_copies_provider_metadata_type_name()
    {
        var fullName =
            "DevTools.NUnit.SampleTests.NamedFixtureSourceTests(\"alpha.rvt\").Fixture_argument_is_preserved";
        var node = HostTestFramework.ToDiscoveredNode(
            new TestingDiscoveredTest(
                fullName,
                "Fixture_argument_is_preserved(\"alpha.rvt\")",
                fullName,
                "DevTools.NUnit.SampleTests.NamedFixtureSourceTests(\"alpha.rvt\")",
                "Fixture_argument_is_preserved",
                new TestingSourceLocation(@"C:\src\FixtureShapeTests.cs", 55),
                "DevTools.NUnit.SampleTests",
                "NamedFixtureSourceTests"));

        Assert.Equal(fullName, node.Uid.Value);
        Assert.Equal("Fixture_argument_is_preserved(\"alpha.rvt\")", node.DisplayName);
        var identity = node.Properties.Single<TestMethodIdentifierProperty>();
        Assert.Equal("DevTools.NUnit.SampleTests", identity.Namespace);
        Assert.Equal("NamedFixtureSourceTests", identity.TypeName);
        Assert.Equal("Fixture_argument_is_preserved", identity.MethodName);
        var location = node.Properties.Single<TestFileLocationProperty>();
        Assert.Equal(@"C:\src\FixtureShapeTests.cs", location.FilePath);
        Assert.Equal(55, location.LineSpan.Start.Line);
    }

    [Fact]
    public void ToDiscoveredNode_omits_identifier_when_provider_did_not_supply_metadata()
    {
        var node = HostTestFramework.ToDiscoveredNode(
            new TestingDiscoveredTest("HostSmokeTests.Arithmetic", "Arithmetic", "HostSmokeTests.Arithmetic"));

        Assert.Equal("HostSmokeTests.Arithmetic", node.Uid.Value);
        Assert.Null(node.Properties.SingleOrDefault<TestMethodIdentifierProperty>());
    }

    [Theory]
    [InlineData("Passed", typeof(PassedTestNodeStateProperty))]
    [InlineData("Failed", typeof(FailedTestNodeStateProperty))]
    [InlineData("Skipped", typeof(SkippedTestNodeStateProperty))]
    [InlineData("Error", typeof(ErrorTestNodeStateProperty))]
    public void ToResultNode_maps_outcomes(string outcome, Type stateType)
    {
        var node = HostTestFramework.ToResultNode(
            new TestingCaseResult("id", "Case", outcome, 12, "msg", null, null, null, [], [], SkipReason: "ignored"));

        Assert.Equal("Case", node.DisplayName);
        Assert.Equal("id", node.Uid.Value);
        Assert.Contains(node.Properties.AsEnumerable(), property => property.GetType() == stateType);
    }

    [Fact]
    public void ToResultNode_uses_test_id_as_uid()
    {
        var discovered = new TestingDiscoveredTest(
            "HostSmokeTests.Arithmetic",
            "Arithmetic",
            "HostSmokeTests.Arithmetic",
            "HostSmokeTests",
            "Arithmetic",
            Namespace: "",
            TypeName: "HostSmokeTests");
        var node = HostTestFramework.ToResultNode(
            new TestingCaseResult(
                "HostSmokeTests.Arithmetic",
                "Arithmetic",
                "Passed",
                12,
                null,
                null,
                null,
                null,
                [],
                [],
                FullName: "HostSmokeTests.Arithmetic"),
            assemblyPath: null,
            [discovered]);

        Assert.Equal("HostSmokeTests.Arithmetic", node.Uid.Value);
        var identity = node.Properties.Single<TestMethodIdentifierProperty>();
        Assert.Equal("HostSmokeTests", identity.TypeName);
        Assert.Equal("Arithmetic", identity.MethodName);
    }

    [Fact]
    public void ToResultNode_copies_discovered_method_identifier()
    {
        const string uid = "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture.Named_one";
        var discovered = new TestingDiscoveredTest(
            uid,
            "Named_one",
            uid,
            "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture",
            "Original_named",
            Namespace: "DevTools.NUnit.Runtime.Fixtures",
            TypeName: "TestNameCaseFixture");

        var discoveredId = HostTestFramework.ToDiscoveredNode(discovered)
            .Properties.Single<TestMethodIdentifierProperty>();
        var resultId = HostTestFramework.ToResultNode(
                new TestingCaseResult(
                    uid,
                    "Named_one",
                    "Passed",
                    1,
                    null,
                    null,
                    null,
                    null,
                    [],
                    [],
                    FullName: uid),
                assemblyPath: null,
                [discovered])
            .Properties.Single<TestMethodIdentifierProperty>();

        Assert.Equal("Original_named", discoveredId.MethodName);
        Assert.Equal(discoveredId.Namespace, resultId.Namespace);
        Assert.Equal(discoveredId.TypeName, resultId.TypeName);
        Assert.Equal(discoveredId.MethodName, resultId.MethodName);
    }

    [Fact]
    public void ToResultNode_without_discovery_omits_method_identifier()
    {
        var identity = HostTestFramework.ToResultNode(
                new TestingCaseResult(
                    "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture.Named_one",
                    "Named_one",
                    "Passed",
                    1,
                    null,
                    null,
                    null,
                    null,
                    [],
                    [],
                    FullName: "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture.Named_one"))
            .Properties.SingleOrDefault<TestMethodIdentifierProperty>();

        Assert.Null(identity);
    }

    [Fact]
    public void ToResultNode_maps_standard_output()
    {
        var node = HostTestFramework.ToResultNode(
            new TestingCaseResult(
                "id",
                "Writes_output",
                "Passed",
                12,
                null,
                null,
                "ERR devtools-nunit-sample-trace\ndevtools-nunit-sample-debug",
                null,
                [],
                []));

        var stdout = node.Properties.Single<StandardOutputProperty>();
        Assert.Contains("devtools-nunit-sample-trace", stdout.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("devtools-nunit-sample-debug", stdout.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void ToRunnerFilter_prefers_selected_uids()
    {
        var filter = new TestNodeUidListFilter([new TestNodeUid("HostSmokeTests.Arithmetic")]);
        var selection = HostTestFramework.ToRunnerFilter(filter, "Intentional_failure_for_demo");
        Assert.Equal(["HostSmokeTests.Arithmetic"], selection.TestIds.ToArray());
        Assert.Null(selection.ProviderPayload);
    }

    [Fact]
    public void ToRunnerFilter_uses_method_name_when_no_uid_list()
    {
        var selection = HostTestFramework.ToRunnerFilter(null, nameFilter: "Arithmetic_runs_inside_host");
        Assert.Equal(["Arithmetic_runs_inside_host"], selection.Names!.ToArray());
        Assert.Empty(selection.TestIds);
        Assert.Null(selection.ProviderPayload);
    }
}

internal sealed class StubHostTestDiscoverer : IHostTestDiscoverer
{
    public IReadOnlyList<TestingDiscoveredTest> Discover(string assemblyPath) =>
        [new TestingDiscoveredTest("Stub.Test", "Test", "Stub.Test")];

    public IReadOnlyList<TestingDiscoveredTest> Discover(
        string assemblyPath,
        TestingDiscoveryOptions options) =>
        Discover(assemblyPath);

    public IReadOnlyList<TestingDiscoveredTest> Select(string assemblyPath, TestingSelection selection) =>
        Discover(assemblyPath);

    public IReadOnlyList<TestingDiscoveredTest> Select(
        string assemblyPath,
        TestingSelection selection,
        TestingDiscoveryOptions options) =>
        Select(assemblyPath, selection);
}

internal sealed class FakeTestRunnerTransport : ITestRunnerTransport
{
    internal TestingRunRequest? LastRequest { get; private set; }

    internal TestingHostOptions? LastHostOptions { get; private set; }

    internal bool Cancelled { get; private set; }

    internal TestingRunResponse? Response { get; set; }

    public TestingRunResponse Run(
        TestingRunRequest request,
        TestingHostOptions hostOptions,
        Action<TestingCaseResult> onResult)
    {
        LastRequest = request;
        LastHostOptions = hostOptions;
        var response = Response ?? new TestingRunResponse(
            request.RunId,
            request.FrameworkId,
            null,
            [],
            TestingCancellationState.None,
            null,
            null);
        foreach (var result in response.Results)
            onResult(result);
        return response;
    }

    public void Cancel(Guid runId) => Cancelled = true;

    public void Dispose()
    {
    }
}

internal sealed class StubConfiguration : IConfiguration
{
    private readonly IReadOnlyDictionary<string, string?> _values;

    internal StubConfiguration(IReadOnlyDictionary<string, string?> values) => _values = values;

    public string? this[string key] =>
        _values.TryGetValue(key, out var value) ? value : null;
}
