using System.Text.Json;
using DevTools.TestAdapter;
using DevTools.Testing.Abstractions;
using DevTools.Testing.Abstractions.Config;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Tests;
using DevTools.Testing.Transport;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Requests;

namespace DevTools.TestAdapter.Tests;

#pragma warning disable TPEXP

[TestClass]
[DoNotParallelize]
public sealed class TestFrameworkTests
{
    private static readonly object DiscoveryProviderLock = new();

    [TestMethod]
    public void ScaleForRun_multiplies_per_test_timeout()
    {
        var options = new TestHostOptions("Revit", "2026", false, 60, 180);
        var scaled = TestFramework.ScaleForRun(options, testCount: 3);
        Assert.AreEqual(60, scaled.PerTestTimeoutSeconds);
        Assert.AreEqual(180, scaled.RequestTimeoutSeconds);
        Assert.AreEqual(60, options.PerTestTimeoutSeconds);
    }

    [TestMethod]
    public void SelectCases_throws_when_provider_is_not_registered()
    {
        lock (DiscoveryProviderLock)
        {
            var previous = TestingDiscovery.Provider;
            var previousMapper = TestingDiscovery.RunMapper;
            TestingDiscovery.Clear();
            try
            {
                var ex = Assert.ThrowsExactly<InvalidOperationException>(
                    () => TestFramework.SelectCases(
                        typeof(TestFrameworkTests).Assembly.Location,
                        TestSelection.All));
                Assert.Contains("TestingDiscovery.Register", ex.Message, StringComparison.Ordinal);
            }
            finally
            {
                RestoreDiscovery(previous, previousMapper);
            }
        }
    }

    [TestMethod]
    public void Run_returns_pass_fail_skip_and_error_from_transport()
    {
        var transport = new FakeTestRunnerTransport
        {
            Response = new TestRunResponse(
                Guid.NewGuid(),
                TestFrameworkId.NUnit,
                "gen",
                [
                    new TestCaseResult("1", "Pass", "Passed", 10, null, null, null, null, [], []),
                    new TestCaseResult("2", "Fail", "Failed", 20, "boom", "at Foo", null, null, [], []),
                    new TestCaseResult("3", "Skip", "Skipped", 0, "ignored", null, null, null, [], []),
                    new TestCaseResult("4", "Err", "Error", 5, "init", null, null, null, [], []),
                ],
                TestCancellationState.None,
                null,
                null),
        };
        var session = new TestRunSession(transport);

        var response = session.Run(
            "C:\\tests\\a.dll",
            new TestHostOptions("Revit", "2026", false, 60, 180),
            TestFrameworkId.NUnit,
            TestSelection.All);

        Assert.AreEqual(TestFrameworkId.NUnit, transport.LastRequest!.FrameworkId);
        Assert.AreSequenceEqual(["Passed", "Failed", "Skipped", "Error"], response.Results.Select(result => result.Outcome).ToArray());
    }

    [TestMethod]
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
                    var previous = TestingDiscovery.Provider;
                    var previousMapper = TestingDiscovery.RunMapper;
                    TestingDiscovery.Register(new StubTestDiscoverer(), PassThroughRunMapper.Instance);
                    try
                    {
                        var nodes = TestFramework.DiscoverNodes(
                            typeof(TestFrameworkTests).Assembly.Location,
                            TestSelection.All);
                        Assert.IsNotNull(nodes);
                        Assert.IsNotEmpty(nodes);
                    }
                    finally
                    {
                        RestoreDiscovery(previous, previousMapper);
                    }
                }
            }
        }
        finally
        {
            try { File.Delete(runnerPath); } catch { /* best effort */ }
        }
    }

    [TestMethod]
    public void Run_sends_nunit_framework_id_to_generic_transport()
    {
        var transport = new FakeTestRunnerTransport();
        var session = new TestRunSession(transport);

        session.Run(
            "C:\\tests\\a.dll",
            new TestHostOptions("Revit", "2026", false, 60, 180),
            TestFrameworkId.NUnit,
            TestSelection.FromTestIds(["HostSmokeTests.Arithmetic"]));

        Assert.AreEqual(TestFrameworkId.NUnit, transport.LastRequest!.FrameworkId);
        Assert.AreSequenceEqual(["HostSmokeTests.Arithmetic"], transport.LastRequest.Selection.TestIds.ToArray());
    }

    [TestMethod]
    public void Run_throws_when_framework_id_is_undefined()
    {
        var session = new TestRunSession(new FakeTestRunnerTransport());
        var ex = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => session.Run(
            "C:\\tests\\a.dll",
            new TestHostOptions("Revit", "2026", false, 60, 180),
            (TestFrameworkId)42,
            TestSelection.All));
        Assert.AreEqual("FrameworkId", ex.ParamName);
    }

    [TestMethod]
    public void Name_filter_round_trips_through_generic_selection()
    {
        var selection = TestFramework.ToRunnerFilter(null, nameFilter: "Arithmetic_runs_inside_host");

        Assert.AreEqual(TestSelectionKind.Names, selection.Kind);
        Assert.AreSequenceEqual(["Arithmetic_runs_inside_host"], selection.Names.ToArray());
    }

    private static void RestoreDiscovery(ITestDiscoverer? provider, ITestRunMapper? mapper)
    {
        if (provider is not null && mapper is not null)
            TestingDiscovery.Register(provider, mapper);
        else
            TestingDiscovery.Clear();
    }
}

[TestClass]
public sealed class ProcessTestRunnerCliTests
{
    [TestMethod]
    public void SerializeExecute_sends_framework_and_test_ids()
    {
        var json = TestRunnerCli.SerializeExecute(
            new TestRunRequest(
                TestingProtocol.CurrentVersion,
                Guid.NewGuid(),
                TestFrameworkId.NUnit,
                new TestAssemblyReference(@"C:\tests\HostTests.dll"),
                TestSelection.FromTestIds(["HostSmokeTests.Arithmetic"])),
            new TestHostOptions("Revit", "2026", true, 60, 180));

        Assert.Contains("\"framework_id\":\"NUnit\"", json, StringComparison.Ordinal);
        Assert.Contains(@"C:\\tests\\HostTests.dll", json, StringComparison.Ordinal);
        Assert.Contains("\"force_launch\":true", json, StringComparison.Ordinal);
        Assert.Contains("HostSmokeTests.Arithmetic", json, StringComparison.Ordinal);
        Assert.DoesNotContain("discover", json, StringComparison.Ordinal);
        Assert.DoesNotContain("runner_path", json, StringComparison.Ordinal);
    }

    [TestMethod]
    public void SerializeExecute_includes_debug_parent_pid()
    {
        var json = TestRunnerCli.SerializeExecute(
            new TestRunRequest(
                TestingProtocol.CurrentVersion,
                Guid.NewGuid(),
                TestFrameworkId.NUnit,
                new TestAssemblyReference(@"C:\tests\HostTests.dll"),
                TestSelection.All),
            new TestHostOptions("Revit", "2026", false, 60, 180, DebugParentPid: 4242));

        Assert.Contains("\"debug_parent_pid\":4242", json, StringComparison.Ordinal);
    }

    [TestMethod]
    public void SerializeExecute_omits_debug_parent_pid_when_absent()
    {
        var json = TestRunnerCli.SerializeExecute(
            new TestRunRequest(
                TestingProtocol.CurrentVersion,
                Guid.NewGuid(),
                TestFrameworkId.NUnit,
                new TestAssemblyReference(@"C:\tests\HostTests.dll"),
                TestSelection.All),
            new TestHostOptions("Revit", "2026", false, 60, 180));

        Assert.DoesNotContain("debug_parent_pid", json, StringComparison.Ordinal);
    }

    [TestMethod]
    public void SerializeExecute_preserves_run_id()
    {
        var runId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var json = TestRunnerCli.SerializeExecute(
            new TestRunRequest(
                TestingProtocol.CurrentVersion,
                runId,
                TestFrameworkId.NUnit,
                new TestAssemblyReference(@"C:\tests\HostTests.dll"),
                TestSelection.FromTestIds(["HostSmokeTests.Arithmetic"])),
            new TestHostOptions("Revit", "2026", true, 60, 180));

        Assert.Contains("\"run_id\":\"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee\"", json, StringComparison.Ordinal);
        Assert.Contains("\"framework_id\":\"NUnit\"", json, StringComparison.Ordinal);
        Assert.Contains("HostSmokeTests.Arithmetic", json, StringComparison.Ordinal);
#pragma warning disable MSTEST0032 // const command name documents the CLI contract.
        Assert.AreEqual(TestRunnerCli.RunCommand, "run");
#pragma warning restore MSTEST0032
    }
}

[TestClass]
public sealed class TestRunSettingsLoaderTests
{
    [TestMethod]
    public void Load_reads_mtp_iconfiguration_keys()
    {
        IConfiguration configuration = new StubConfiguration(new Dictionary<string, string?>
        {
            [TestConfig.Keys.Configuration(TestConfig.Keys.HostName)] = "Civil3D",
            [TestConfig.Keys.Configuration(TestConfig.Keys.HostVersion)] = "2026",
            [TestConfig.Keys.Configuration(TestConfig.Keys.ForceLaunch)] = "true",
            [TestConfig.Keys.Configuration(TestConfig.Keys.PerTestTimeoutSeconds)] = "90",
            [TestConfig.Keys.Configuration(TestConfig.Keys.LaunchTimeoutSeconds)] = "240",
            [TestConfig.Keys.Configuration(TestConfig.Keys.RunnerPath)] = @"C:\Runner.exe",
            [TestConfig.Keys.Configuration(TestConfig.Keys.FrameworkId)] = "nunit",
        });

        var options = TestRunSettingsLoader.Load(configuration);

        Assert.AreEqual("Civil3D", options.Host.HostName);
        Assert.AreEqual("2026", options.Host.HostVersion);
        Assert.IsTrue(options.Host.ForceLaunch);
        Assert.AreEqual(90, options.Host.PerTestTimeoutSeconds);
        Assert.AreEqual(240, options.Host.LaunchTimeoutSeconds);
        Assert.AreEqual(TestFrameworkId.NUnit, options.FrameworkId);
        Assert.AreEqual(@"C:\Runner.exe", options.RunnerPath);
    }

    [TestMethod]
    public void Load_throws_when_framework_id_is_empty()
    {
        IConfiguration configuration = new StubConfiguration(new Dictionary<string, string?>
        {
            [TestConfig.Keys.Configuration(TestConfig.Keys.HostName)] = "Revit",
            [TestConfig.Keys.Configuration(TestConfig.Keys.HostVersion)] = "2026",
            [TestConfig.Keys.Configuration(TestConfig.Keys.PerTestTimeoutSeconds)] = "60",
            [TestConfig.Keys.Configuration(TestConfig.Keys.LaunchTimeoutSeconds)] = "180",
            [TestConfig.Keys.Configuration(TestConfig.Keys.FrameworkId)] = "",
        });

        var ex = Assert.ThrowsExactly<InvalidOperationException>(() => TestRunSettingsLoader.Load(configuration));
        Assert.Contains("frameworkId", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void Load_throws_when_devtools_section_is_missing()
    {
        var ex = Assert.ThrowsExactly<InvalidOperationException>(
            () => TestRunSettingsLoader.Load(new StubConfiguration(new Dictionary<string, string?>())));
        Assert.Contains(TestConfig.FileName, ex.Message, StringComparison.Ordinal);
        Assert.Contains(TestConfig.SectionName, ex.Message, StringComparison.Ordinal);
    }
}

[TestClass]
public sealed class TestNodeMapperTests
{
    [TestMethod]
    public void CreateErrorNode_sets_error_state()
    {
        var node = TestNodeProperties.CreateErrorNode("uid", "display", new InvalidOperationException("boom"));
        Assert.AreEqual("uid", node.Uid.Value);
        Assert.AreEqual("display", node.DisplayName);
        Assert.IsNotNull(node.Properties.SingleOrDefault<ErrorTestNodeStateProperty>());
    }

    [TestMethod]
    public void ToDiscoveredNode_uses_test_id_as_uid()
    {
        var node = TestFramework.ToDiscoveredNode(
            new TestDiscoveredTest(
                "HostSmokeTests.Arithmetic",
                "Arithmetic",
                "HostSmokeTests.Arithmetic",
                "HostSmokeTests",
                "Arithmetic",
                Namespace: "",
                TypeName: "HostSmokeTests"));

        Assert.AreEqual("HostSmokeTests.Arithmetic", node.Uid.Value);
        Assert.AreEqual("Arithmetic", node.DisplayName);
        Assert.IsNotNull(node.Properties.SingleOrDefault<DiscoveredTestNodeStateProperty>());
        var identity = node.Properties.Single<TestMethodIdentifierProperty>();
        Assert.AreEqual("HostSmokeTests", identity.TypeName);
        Assert.AreEqual("Arithmetic", identity.MethodName);
        Assert.IsEmpty(identity.ParameterTypeFullNames);
        Assert.AreEqual("System.Void", identity.ReturnTypeFullName);
    }

    [TestMethod]
    public void ToDiscoveredNode_copies_parameter_and_return_types()
    {
        var node = TestFramework.ToDiscoveredNode(
            new TestDiscoveredTest(
                "ArgumentsDataSourceTests.Named_basis_length_is_one",
                "Unit_X",
                "DevTools.TUnit.SampleTests.ArgumentsDataSourceTests.Named_basis_length_is_one",
                "DevTools.TUnit.SampleTests.ArgumentsDataSourceTests",
                "Named_basis_length_is_one",
                Namespace: "DevTools.TUnit.SampleTests",
                TypeName: "ArgumentsDataSourceTests",
                ParameterTypeFullNames: ["System.Double", "System.Double", "System.Double"],
                ReturnTypeFullName: "System.Threading.Tasks.Task"));

        var identity = node.Properties.Single<TestMethodIdentifierProperty>();
        Assert.AreEqual("Named_basis_length_is_one", identity.MethodName);
        Assert.AreSequenceEqual(["System.Double", "System.Double", "System.Double"], identity.ParameterTypeFullNames);
        Assert.AreEqual("System.Threading.Tasks.Task", identity.ReturnTypeFullName);
    }

    [TestMethod]
    public void ToDiscoveredNode_copies_provider_metadata_type_name()
    {
        var fullName =
            "DevTools.NUnit.SampleTests.NamedFixtureSourceTests(\"alpha.rvt\").Fixture_argument_is_preserved";
        var node = TestFramework.ToDiscoveredNode(
            new TestDiscoveredTest(
                fullName,
                "Fixture_argument_is_preserved(\"alpha.rvt\")",
                fullName,
                "DevTools.NUnit.SampleTests.NamedFixtureSourceTests(\"alpha.rvt\")",
                "Fixture_argument_is_preserved",
                new TestSourceLocation(@"C:\src\FixtureShapeTests.cs", 55),
                "DevTools.NUnit.SampleTests",
                "NamedFixtureSourceTests"));

        Assert.AreEqual(fullName, node.Uid.Value);
        Assert.AreEqual("Fixture_argument_is_preserved(\"alpha.rvt\")", node.DisplayName);
        var identity = node.Properties.Single<TestMethodIdentifierProperty>();
        Assert.AreEqual("DevTools.NUnit.SampleTests", identity.Namespace);
        Assert.AreEqual("NamedFixtureSourceTests", identity.TypeName);
        Assert.AreEqual("Fixture_argument_is_preserved", identity.MethodName);
        var location = node.Properties.Single<TestFileLocationProperty>();
        Assert.AreEqual(@"C:\src\FixtureShapeTests.cs", location.FilePath);
        Assert.AreEqual(55, location.LineSpan.Start.Line);
    }

    [TestMethod]
    public void ToDiscoveredNode_omits_identifier_when_provider_did_not_supply_metadata()
    {
        var node = TestFramework.ToDiscoveredNode(
            new TestDiscoveredTest("HostSmokeTests.Arithmetic", "Arithmetic", "HostSmokeTests.Arithmetic"));

        Assert.AreEqual("HostSmokeTests.Arithmetic", node.Uid.Value);
        Assert.IsNull(node.Properties.SingleOrDefault<TestMethodIdentifierProperty>());
    }

    [TestMethod]
    [DataRow("Passed", typeof(PassedTestNodeStateProperty))]
    [DataRow("Failed", typeof(FailedTestNodeStateProperty))]
    [DataRow("Skipped", typeof(SkippedTestNodeStateProperty))]
    [DataRow("Error", typeof(ErrorTestNodeStateProperty))]
    public void ToResultNode_maps_outcomes(string outcome, Type stateType)
    {
        var node = TestFramework.ToResultNode(
            new TestCaseResult("id", "Case", outcome, 12, "msg", null, null, null, [], [], SkipReason: "ignored"));

        Assert.AreEqual("Case", node.DisplayName);
        Assert.AreEqual("id", node.Uid.Value);
        Assert.IsTrue(node.Properties.AsEnumerable().Any(property => property.GetType() == stateType));
    }

    [TestMethod]
    public void ToResultNode_uses_test_id_as_uid()
    {
        var discovered = new TestDiscoveredTest(
            "HostSmokeTests.Arithmetic",
            "Arithmetic",
            "HostSmokeTests.Arithmetic",
            "HostSmokeTests",
            "Arithmetic",
            Namespace: "",
            TypeName: "HostSmokeTests");
        var node = TestFramework.ToResultNode(
            new TestCaseResult(
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

        Assert.AreEqual("HostSmokeTests.Arithmetic", node.Uid.Value);
        var identity = node.Properties.Single<TestMethodIdentifierProperty>();
        Assert.AreEqual("HostSmokeTests", identity.TypeName);
        Assert.AreEqual("Arithmetic", identity.MethodName);
    }

    [TestMethod]
    public void ToResultNode_copies_discovered_method_identifier()
    {
        const string uid = "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture.Named_one";
        var discovered = new TestDiscoveredTest(
            uid,
            "Named_one",
            uid,
            "DevTools.NUnit.Runtime.Fixtures.TestNameCaseFixture",
            "Original_named",
            Namespace: "DevTools.NUnit.Runtime.Fixtures",
            TypeName: "TestNameCaseFixture");

        var discoveredId = TestFramework.ToDiscoveredNode(discovered)
            .Properties.Single<TestMethodIdentifierProperty>();
        var resultId = TestFramework.ToResultNode(
                new TestCaseResult(
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

        Assert.AreEqual("Original_named", discoveredId.MethodName);
        Assert.AreEqual(discoveredId.Namespace, resultId.Namespace);
        Assert.AreEqual(discoveredId.TypeName, resultId.TypeName);
        Assert.AreEqual(discoveredId.MethodName, resultId.MethodName);
    }

    [TestMethod]
    public void ToResultNode_without_discovery_omits_method_identifier()
    {
        var identity = TestFramework.ToResultNode(
                new TestCaseResult(
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

        Assert.IsNull(identity);
    }

    [TestMethod]
    public void ToResultNode_maps_standard_output()
    {
        var node = TestFramework.ToResultNode(
            new TestCaseResult(
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

    [TestMethod]
    public void ToRunnerFilter_prefers_selected_uids()
    {
        var filter = new TestNodeUidListFilter([new TestNodeUid("HostSmokeTests.Arithmetic")]);
        var selection = TestFramework.ToRunnerFilter(filter, "Intentional_failure_for_demo");
        Assert.AreSequenceEqual(["HostSmokeTests.Arithmetic"], selection.TestIds.ToArray());
        Assert.AreEqual(TestSelectionKind.TestIds, selection.Kind);
    }

    [TestMethod]
    public void ToRunnerFilter_unwraps_composite_uid_list()
    {
        var uidFilter = new TestNodeUidListFilter([new TestNodeUid("HostSmokeTests.Arithmetic")]);
        var filter = new CompositeTestExecutionFilter(
            TestExecutionFilterOperator.And,
            [uidFilter, new NopFilter()]);
        var selection = TestFramework.ToRunnerFilter(filter);
        Assert.AreSequenceEqual(["HostSmokeTests.Arithmetic"], selection.TestIds.ToArray());
        Assert.AreEqual(TestSelectionKind.TestIds, selection.Kind);
    }

    [TestMethod]
    public void ToRunnerFilter_empty_uid_list_is_constrained()
    {
        var selection = TestFramework.ToRunnerFilter(new TestNodeUidListFilter([]));
        Assert.AreEqual(TestSelectionKind.TestIds, selection.Kind);
        Assert.IsEmpty(selection.TestIds);
        Assert.IsTrue(selection.IsConstrained);
    }

    [TestMethod]
    public void ToDiscoverFilter_empty_uid_list_is_all()
    {
        var selection = TestFramework.ToDiscoverFilter(new TestNodeUidListFilter([]));
        Assert.AreEqual(TestSelectionKind.All, selection.Kind);
        Assert.IsFalse(selection.IsConstrained);
    }

    [TestMethod]
    public void MtpTreePaths_include_namespace_type_method()
    {
        var leaf = new TestDiscoveredTest(
            "Ns.Box.Bottom_corners_share_min_z(-12.3,45.6)",
            "Bottom_corners_share_min_z(-12.3,45.6)",
            "Ns.Box.Bottom_corners_share_min_z(-12.3,45.6)",
            "Ns.Box",
            "Bottom_corners_share_min_z",
            Namespace: "Ns",
            TypeName: "Box");

        var paths = TestFramework.MtpTreePaths(leaf).ToArray();
        Assert.Contains("/Ns/Box/Bottom_corners_share_min_z", paths, StringComparer.Ordinal);
        Assert.Contains("/" + Uri.EscapeDataString(leaf.TestId), paths, StringComparer.Ordinal);
    }

    [TestMethod]
    public void ToRunnerFilter_uses_method_name_when_no_uid_list()
    {
        var selection = TestFramework.ToRunnerFilter(null, nameFilter: "Arithmetic_runs_inside_host");
        Assert.AreSequenceEqual(["Arithmetic_runs_inside_host"], selection.Names.ToArray());
        Assert.IsEmpty(selection.TestIds);
        Assert.AreEqual(TestSelectionKind.Names, selection.Kind);
    }
}

internal sealed class StubTestDiscoverer : ITestDiscoverer
{
    public IReadOnlyList<TestDiscoveredTest> Discover(string assemblyPath, TestSelection selection) =>
        [new TestDiscoveredTest("Stub.Test", "Test", "Stub.Test")];
}

internal sealed class FakeTestRunnerTransport : ITestRunnerTransport
{
    internal TestRunRequest? LastRequest { get; private set; }

    internal TestHostOptions? LastHostOptions { get; private set; }

    internal bool Cancelled { get; private set; }

    internal TestRunResponse? Response { get; set; }

    internal TestEvent[]? StreamedEvents { get; set; }

    internal Exception? RunException { get; set; }

    internal ManualResetEventSlim? RunEntered { get; set; }

    internal ManualResetEventSlim? BlockRun { get; set; }

    public TestRunResponse Run(
        TestRunRequest request,
        TestHostOptions hostOptions,
        Action<TestEvent> onEvent)
    {
        LastRequest = request;
        LastHostOptions = hostOptions;
        RunEntered?.Set();
        BlockRun?.Wait();
        if (RunException is not null)
            throw RunException;
        var response = Response ?? new TestRunResponse(
            request.RunId,
            request.FrameworkId,
            null,
            [],
            TestCancellationState.None,
            null,
            null);
        var events = StreamedEvents
            ?? response.Results
                .Select(result => new TestEvent(
                    request.RunId,
                    TestEventKinds.Case,
                    result,
                    null,
                    null,
                    TestCancellationState.None))
                .ToArray();
        foreach (var testingEvent in events)
            onEvent(testingEvent);

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
